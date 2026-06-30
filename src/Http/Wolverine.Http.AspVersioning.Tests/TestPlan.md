# Test Plan — `Wolverine.Http.AspVersioning`

> **Audience:** Claude Code. This document is a work specification, not background reading. Execute the phases in order. Every test case below has a stable ID (e.g. `RES-1`) — implement them as discrete tests and keep the IDs traceable (in test names, comments, or commit messages) so coverage can be audited against this plan.

---

## Step 0 — Conform to `Wolverine.Http.Tests` (MANDATORY, do this first)

Before writing a single test, read the sibling `Wolverine.Http.Tests` project and **mirror its conventions exactly**. The integration must look like it was written by the same team. Do not introduce new patterns, libraries, or styles.

Discover and adopt, from the actual project (not from assumption):

- **Test framework** and version (attributes, theory/data-row mechanism).
- **Assertion library** — use whatever the project uses; do not mix in a second one.
- **HTTP integration host helper** and fixture base classes — the bootstrapping pattern for a running Wolverine host, class/collection fixtures, and any shared `AppFixture`-style setup. Reuse these; do not hand-roll a host if a helper exists.
- **Naming conventions** — test method naming, file naming, folder layout, namespaces.
- **Sample handler/endpoint definitions** already present in the test project — reuse or imitate them rather than inventing a new style.
- **Project-wide settings** — `.editorconfig`, nullable context, file-scoped namespaces, `Directory.Build.props`, analyzers. Match formatting precisely.
- **Build/run commands** — how the repo runs tests (build script vs `dotnet test`), and any test categorization/traits.
- **Existing test dependencies** — do not add a NuGet package the test project doesn't already reference without flagging it explicitly.

> **Likely stack (verify against the repo before relying on it):** JasperFx projects commonly use xUnit + Shouldly for assertions and Alba for in-process HTTP testing, with file-scoped namespaces and nullable enabled. Treat this only as a hint for what to look for — **the actual project is authoritative.**

**Rule:** if a convention is ambiguous, match the nearest existing example in `Wolverine.Http.Tests` and note which file you patterned after. When the repo's convention conflicts with anything in this plan's examples, the repo wins on style; this plan governs *what* to test, the repo governs *how it's written*.

---

## Context — what is under test

The package is a **translator**. A single `IHttpPolicy` reads API-version attributes off each handler method/class and attaches Asp.Versioning metadata (`ApiVersionSet`, per-endpoint version providers, `ApiVersionNeutralAttribute`) to each `HttpChain`. Everything downstream — request matching, `400`/`404` responses, response headers — is Asp.Versioning's job. Everything upstream — endpoint discovery, route generation — is Wolverine's job.

**The integration is the narrow band in between: attributes in, attached metadata out.**

---

## Testing philosophy (the golden rule)

**Assert on the metadata the integration produces. Treat the libraries' runtime behavior only as a wiring tripwire, never as the subject of a test.**

Heuristic for every proposed test — *the deletion test:* if the test would still pass and still be meaningful after deleting all of this package's code and keeping only Wolverine + Asp.Versioning, it is testing the wrong thing. Delete it or move it.

This keeps the suite from degenerating into a re-test of `Wolverine.Versioning` or `Asp.Versioning`.

---

## Prerequisite — ensure the testable seam exists

Most logic must be unit-testable without a running host. Confirm (or refactor toward) this shape in the product code before writing Tier 1:

- `Apply` is a thin adapter: it extracts `(MethodInfo, route text)` from each chain, runs a **host-free pipeline** (resolve → merge advertised → group → bucket → build set → compute per-chain roles), then attaches results to the chains.
- The pipeline functions take plain inputs (`MethodInfo`, strings, the resolver outputs) and return plain results — no `HttpChain` dependency.

If this seam does not exist, raise it before proceeding; Tier 1 depends on it. Tier 1 tests should drive the resolvers with tiny fixture handler classes and `typeof(T).GetMethod(...)`.

---

## Tier 1 — Pure logic (exhaustive, no host)

This is where most coverage and nearly all branch complexity live, because this is the logic the package actually owns. Aim for **branch coverage** here. Use a small attribute-permutation test-data builder and minimal fixture handler classes to keep it readable.

### Resolution & precedence (`RES`)

- **RES-1** Method `[ApiVersion("2.0")]` + class `[ApiVersion("1.0")]` → resolved versions are `{2.0}` only. Explicitly assert the result is **not** `{1.0, 2.0}` (method *replaces* class; this is the exact bug that arises from leaning on Asp.Versioning's union semantics).
- **RES-2** Method `[MapToApiVersion("1.0")]` + class `[ApiVersion("1.0")]`,`[ApiVersion("2.0")]` → resolved is `{1.0}`; deprecation is sourced from the class attributes.
- **RES-3** Class `[ApiVersion("1.0", Deprecated = true)]` + method `[MapToApiVersion("1.0")]` → `1.0` carries the `Deprecated` option.
- **RES-4** Method `[ApiVersion("2.0")]` + class `[ApiVersionNeutral]` → result is versioned `{2.0}`, **not** neutral (method overrides class neutrality).
- **RES-5** Class `[ApiVersion]` only, no method attributes → resolved equals the class versions.
- **RES-6** No version attributes anywhere → resolver yields empty; the chain is excluded from versioning entirely.
- **RES-7** A version that is both served and mapped → `ApiVersionProviderOptions` accumulates correctly (the `Mapped` bit is present; deprecation sets the `Deprecated` bit).
- **RES-8** *(guard)* Method declares both `[ApiVersion]` and `[MapToApiVersion]` → throws `InvalidOperationException`; assert the message names the offending method.
- **RES-9** *(guard)* Method has `[MapToApiVersion]` but the class has no `[ApiVersion]` → throws; assert the message names the method and class.
- **RES-10** *(guard)* `[ApiVersion]` and `[ApiVersionNeutral]` together on the same target (test both method-level and class-level) → throws.
- **RES-11** *(guard)* `[MapToApiVersion]` lists a version not declared on the class → throws (subset violation).

> The guard tests (`RES-8`–`RES-11`) double as executable documentation of the authoring rules. Assert on the specific exception type **and** that the message identifies the offending member — not just that *something* threw.

### Advertised-version merge (`ADV`)

- **ADV-1** Method `[AdvertiseApiVersions("3.0")]` + class `[AdvertiseApiVersions("4.0")]` → union `{3.0, 4.0}`, no precedence applied.
- **ADV-2** Same advertised version on both method and class → appears exactly once.
- **ADV-3** Class advertises `3.0` as deprecated, method advertises `3.0` as current → `3.0` is advertised-**deprecated** (deprecation ORs across sources).
- **ADV-4** A version advertised somewhere but **implemented** by any chain in the group → resolves to implemented; the advertised label is dropped (implemented-beats-advertised).
- **ADV-5** A version advertised only (implemented nowhere) → remains advertised and folds into the supported bucket (or deprecated bucket if advertised-deprecated) at the set level.

### Grouping (`GRP`)

- **GRP-1** Two handlers, same route, versions `1.0` and `2.0` → one group.
- **GRP-2** Routes `"/Orders"` and `"/orders"` → one group (`OrdinalIgnoreCase`).
- **GRP-3** Routes `"/orders"` and `"orders/"` → one group (slash normalization).
- **GRP-4** Routes `"/orders"` and `"/customers"` → two groups.
- **GRP-5** `GET /orders` (v1) + `POST /orders` (v2) → **one** group (verb is deliberately not part of the key). Assert the resulting version space is the union `{1.0, 2.0}` and document in the test that this over-advertisement across verbs is the **accepted, intentional** tradeoff of route-only grouping — so it reads as a pinned expectation, not a latent surprise.
- **GRP-6** A neutral chain sharing a route with versioned chains → partitioned out of the versioned group; it does not contribute to the set.

### Set-building & bucketing (`SET`)

- **SET-1** Mixed supported and deprecated versions → correct two-bucket partition.
- **SET-2** Same version supported by one chain and deprecated by another → **deprecated wins**; the version appears only in the deprecated bucket. (High value: this is the contradiction the `ApiVersionModel` will *not* resolve on its own — it unions supported and deprecated independently with no cross-dedup.)
- **SET-3** Advertised folds into the supported bucket and advertised-deprecated into the deprecated bucket — there is no separate "advertised" list at the set level.
- **SET-4** Exactly one `ApiVersionSet` instance is shared across all chains in a group (assert reference equality of the attached set).
- **SET-5** An all-neutral group → no set is built; each chain receives `ApiVersionNeutralAttribute`.
- **SET-6** An empty/unversioned group → no set and no version metadata.
- **SET-7** Duplicate identical contributions (same version, same deprecation state, from multiple chains) → collapse to a single entry; no exception. (Confirms duplicates are harmless so the fold is only required for the *conflict* case, not for dedup.)

---

## Tier 2 — Policy output (metadata assertions)

Run the full `Apply` over a realistic set of chains and assert on **what got attached**. This covers the assembly/attachment that Tier 1's pure functions do not. Stop at "the correct metadata is present"; do not assert how the matcher will later interpret it.

- **OUT-1** `WithApiVersionSet` is applied exactly once per versioned chain.
- **OUT-2** Per-chain provider roles are correct: served → `None`; served-deprecated → `Deprecated`; mapped → `Mapped`; advertised → `Advertised`; advertised-deprecated → `Advertised | Deprecated`.
- **OUT-3** A neutral chain has `ApiVersionNeutralAttribute` attached and **no** `ApiVersionSet`.
- **OUT-4** An unversioned chain is left completely untouched — no version metadata added at all. (Easy to regress; assert the metadata collection gained nothing version-related.)
- **OUT-5** The set is attached before the per-chain provider conventions on the same chain. If attachment order is observable in metadata, assert it here; otherwise this is covered by `E2E-2`.

---

## Tier 3 — End-to-end wiring (curated, deliberately small)

A real test host: Wolverine + `AddApiVersioning`, a handful of endpoints, real HTTP requests via the project's standard integration-host helper. **The point is not to test version matching** — it is to prove the cross-library assumptions that unit tests structurally cannot. Keep each to one or two tests.

- **E2E-1** *(the core spike)* Directly-attached `ApiVersionMetadata` is honored by `ApiVersionMatcherPolicy`: a request for v1 reaches the v1 endpoint and a request for v2 reaches the v2 endpoint at the same route. If this fails, the whole approach is invalid — make this test loud and prominent.
- **E2E-2** Set-before-conventions ordering does not trip `NoVersionSet` at host build time (the app starts cleanly with versioned Wolverine endpoints).
- **E2E-3** A Wolverine endpoint is not treated as "grouped," so `WithApiVersionSet` does not throw `MultipleVersionSets` even with `AddApiVersioning` registered in the container.
- **E2E-4** A version that the bucketing marked deprecated-wins appears in the `api-deprecated-versions` response header and **not** in `api-supported-versions` (requires `ReportApiVersions`). This proves the fold actually prevents the contradictory-headers outcome the `ApiVersionModel` source otherwise permits.

> Pin the `Asp.Versioning.*` and `Wolverine` package versions for this project. Tier 3 rides on internal behaviors (the endpoint finalizer, `ApiVersionMetadata`/`ApiVersionModel` constructor shapes, the matcher reading attached metadata) that are **not** part of those libraries' public contracts. This tier is the regression canary that must break loudly on a dependency upgrade.

---

## Explicitly OUT OF SCOPE — do NOT test

These look like coverage but are re-tests of the dependencies (they fail the deletion test):

- The matcher returning `400`/`404`/`405` across a matrix of requested versions.
- `ApiVersion` parsing, equality, comparison, or string formatting.
- Exhaustive `api-supported-versions` / `api-deprecated-versions` contents for every version combination — one or two checks in Tier 3 (`E2E-4`) suffice.
- Wolverine's endpoint discovery, route generation, or handler binding.
- OpenAPI document contents, grouping, or per-version document generation.
- The libraries' own attribute semantics (e.g. what `[ApiVersionNeutral]` *means* to Asp.Versioning).

---

## Coverage expectations & definition of done

- **Tier 1:** target **branch coverage** of the resolvers, advertised-merge, grouping, and bucketing. Every branch there is a decision the package authored.
- **Tiers 2 & 3:** representative and curated, not exhaustive. Their value is pinning the seams and the cross-library assumptions, not line count.
- **The real bar:** *every decision the integration makes has exactly one test that fails if that decision changes.* Prefer this over a coverage percentage.
- Every test ID in this plan is implemented, or its omission is explicitly justified.

---

## Execution & maintenance notes

- Run the suite using the repo's standard mechanism discovered in Step 0; do not introduce a new runner or script.
- Do not add test dependencies that `Wolverine.Http.Tests` does not already use without flagging it.
- Organize files, namespaces, and fixtures to match the sibling project; a reviewer should not be able to tell which project a test file came from by style alone.
- When a dependency upgrade breaks Tier 3, treat it as a signal to re-verify the internal-behavior assumptions, not as a flaky test to silence.