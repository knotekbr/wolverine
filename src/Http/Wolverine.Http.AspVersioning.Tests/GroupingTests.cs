using Asp.Versioning;
using Shouldly;

namespace Wolverine.Http.AspVersioning.Tests;

// ---------- Fixture handlers (internal → invisible to host endpoint discovery) ----------

[ApiVersion("1.0")]
internal class Grp1FirstHandler
{
    [WolverineGet("/grp1")]
    public string Get() => "v1";
}

[ApiVersion("2.0")]
internal class Grp1SecondHandler
{
    [WolverineGet("/grp1")]
    public string Get() => "v2";
}

[ApiVersion("1.0")]
internal class Grp2UpperHandler
{
    [WolverineGet("/Grp2Orders")]
    public string Get() => "upper";
}

[ApiVersion("2.0")]
internal class Grp2LowerHandler
{
    [WolverineGet("/grp2orders")]
    public string Get() => "lower";
}

[ApiVersion("1.0")]
internal class Grp3LeadingSlashHandler
{
    [WolverineGet("/grp3")]
    public string Get() => "leading";
}

[ApiVersion("2.0")]
internal class Grp3TrailingSlashHandler
{
    [WolverineGet("grp3/")]
    public string Get() => "trailing";
}

[ApiVersion("1.0")]
internal class Grp4OrdersHandler
{
    [WolverineGet("/grp4-orders")]
    public string Get() => "orders";
}

// Distinct version (2.0) from Grp4OrdersHandler so the two route groups are observably different
// in their aggregate version space (a single group would union to {1.0, 2.0}).
[ApiVersion("2.0")]
internal class Grp4CustomersHandler
{
    [WolverineGet("/grp4-customers")]
    public string Get() => "customers";
}

[ApiVersion("1.0")]
internal class Grp5GetHandler
{
    [WolverineGet("/grp5")]
    public string Get() => "get-v1";
}

[ApiVersion("2.0")]
internal class Grp5PostHandler
{
    [WolverinePost("/grp5")]
    public string Post() => "post-v2";
}

[ApiVersionNeutral]
internal class Grp6NeutralHandler
{
    [WolverineGet("/grp6")]
    public string Get() => "neutral";
}

[ApiVersion("1.0")]
internal class Grp6VersionedHandler
{
    [WolverineGet("/grp6")]
    public string Get() => "v1";
}

// ---------- Tests ----------

/// <summary>
/// Tier 1 — GRP: route-only grouping performed by <see cref="AspVersioningPolicy"/>. The finalizer
/// consumes the shared <c>ApiVersionSet</c> into each chain's <see cref="ApiVersionMetadata"/>, so the
/// observable signal of grouping is the aggregate (group-wide) version space — identical across chains
/// in one group, and disjoint across separate groups.
/// </summary>
public class GroupingTests
{
    // GRP-1 — two handlers, same route, versions 1.0 and 2.0 → one group whose aggregate version space
    // is the union, seen identically by both chains.
    [Fact]
    public void GRP1_same_route_distinct_versions_form_one_group()
    {
        var first = VersioningHarness.ChainFor<Grp1FirstHandler>(x => x.Get());
        var second = VersioningHarness.ChainFor<Grp1SecondHandler>(x => x.Get());

        VersioningHarness.Apply(first, second);

        var union = new[] { new ApiVersion(1, 0), new ApiVersion(2, 0) };
        first.GroupModel().SupportedApiVersions.ShouldBe(union, ignoreOrder: true);
        second.GroupModel().SupportedApiVersions.ShouldBe(union, ignoreOrder: true);
    }

    // GRP-2 — "/Grp2Orders" and "/grp2orders" → one group (OrdinalIgnoreCase route comparison):
    // both chains see the same {1.0, 2.0} aggregate.
    [Fact]
    public void GRP2_routes_differing_only_by_case_form_one_group()
    {
        var upper = VersioningHarness.ChainFor<Grp2UpperHandler>(x => x.Get());
        var lower = VersioningHarness.ChainFor<Grp2LowerHandler>(x => x.Get());

        VersioningHarness.Apply(upper, lower);

        var union = new[] { new ApiVersion(1, 0), new ApiVersion(2, 0) };
        upper.GroupModel().SupportedApiVersions.ShouldBe(union, ignoreOrder: true);
        lower.GroupModel().SupportedApiVersions.ShouldBe(union, ignoreOrder: true);
    }

    // GRP-3 — "/grp3" and "grp3/" → one group (leading/trailing slash normalization).
    [Fact]
    public void GRP3_routes_differing_only_by_slashes_form_one_group()
    {
        var leading = VersioningHarness.ChainFor<Grp3LeadingSlashHandler>(x => x.Get());
        var trailing = VersioningHarness.ChainFor<Grp3TrailingSlashHandler>(x => x.Get());

        VersioningHarness.Apply(leading, trailing);

        var union = new[] { new ApiVersion(1, 0), new ApiVersion(2, 0) };
        leading.GroupModel().SupportedApiVersions.ShouldBe(union, ignoreOrder: true);
        trailing.GroupModel().SupportedApiVersions.ShouldBe(union, ignoreOrder: true);
    }

    // GRP-4 — "/grp4-orders" (1.0) and "/grp4-customers" (2.0) → two distinct groups: each chain's
    // aggregate version space contains only its own group's version, not the other's.
    [Fact]
    public void GRP4_distinct_routes_form_distinct_groups()
    {
        var orders = VersioningHarness.ChainFor<Grp4OrdersHandler>(x => x.Get());
        var customers = VersioningHarness.ChainFor<Grp4CustomersHandler>(x => x.Get());

        VersioningHarness.Apply(orders, customers);

        orders.GroupModel().SupportedApiVersions.ShouldBe(new[] { new ApiVersion(1, 0) });
        customers.GroupModel().SupportedApiVersions.ShouldBe(new[] { new ApiVersion(2, 0) });
    }

    // GRP-5 — GET /grp5 (v1) + POST /grp5 (v2) → ONE group; the HTTP verb is deliberately not part
    // of the grouping key. The resulting version space is the union {1.0, 2.0}. This cross-verb
    // over-advertisement is the accepted, intentional tradeoff of route-only grouping — pinned here
    // so it reads as an expectation, not a latent surprise.
    [Fact]
    public void GRP5_same_route_different_verbs_form_one_group_with_union_version_space()
    {
        var get = VersioningHarness.ChainFor<Grp5GetHandler>(x => x.Get());
        var post = VersioningHarness.ChainFor<Grp5PostHandler>(x => x.Post());

        VersioningHarness.Apply(get, post);

        var union = new[] { new ApiVersion(1, 0), new ApiVersion(2, 0) };
        get.GroupModel().SupportedApiVersions.ShouldBe(union, ignoreOrder: true);
        post.GroupModel().SupportedApiVersions.ShouldBe(union, ignoreOrder: true);
    }

    // GRP-6 — a neutral chain sharing a route with versioned chains is partitioned out of the
    // versioned group: it does not contribute any version to the set.
    [Fact]
    public void GRP6_neutral_chain_does_not_contribute_to_versioned_group()
    {
        var neutral = VersioningHarness.ChainFor<Grp6NeutralHandler>(x => x.Get());
        var versioned = VersioningHarness.ChainFor<Grp6VersionedHandler>(x => x.Get());

        VersioningHarness.Apply(neutral, versioned);

        versioned.GroupModel().SupportedApiVersions.ShouldBe(new[] { new ApiVersion(1, 0) });
        neutral.VersionMetadata()!.IsApiVersionNeutral.ShouldBeTrue();
    }
}
