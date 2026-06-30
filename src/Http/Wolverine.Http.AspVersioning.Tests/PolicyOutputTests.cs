using Asp.Versioning;
using Shouldly;

namespace Wolverine.Http.AspVersioning.Tests;

// ---------- Fixture handlers ----------

[ApiVersion("1.0")]
internal class Out1VersionedHandler
{
    [WolverineGet("/out1")]
    public string Get() => "v1";
}

// One chain per provider role, all sharing route "/out2" so they land in one group/set.
[ApiVersion("1.0")]
internal class Out2ServedHandler
{
    [WolverineGet("/out2")]
    public string Served() => "served";
}

[ApiVersion("2.0", Deprecated = true)]
internal class Out2DeprecatedHandler
{
    [WolverineGet("/out2")]
    public string Deprecated() => "deprecated";
}

[ApiVersion("3.0")]
internal class Out2MappedHandler
{
    [MapToApiVersion("3.0")]
    [WolverineGet("/out2")]
    public string Mapped() => "mapped";
}

[AdvertiseApiVersions("4.0")]
internal class Out2AdvertisedHandler
{
    [WolverineGet("/out2")]
    public string Advertised() => "advertised";
}

[AdvertiseApiVersions("5.0", Deprecated = true)]
internal class Out2AdvertisedDeprecatedHandler
{
    [WolverineGet("/out2")]
    public string AdvertisedDeprecated() => "advertised-deprecated";
}

[ApiVersionNeutral]
internal class Out3NeutralHandler
{
    [WolverineGet("/out3")]
    public string Get() => "neutral";
}

internal class Out4UnversionedHandler
{
    [WolverineGet("/out4")]
    public string Get() => "unversioned";
}

[ApiVersion("1.0")]
internal class Out5VersionedHandler
{
    [WolverineGet("/out5")]
    public string Get() => "v1";
}

// ---------- Tests ----------

/// <summary>
/// Tier 2 — OUT: run the full <see cref="AspVersioningPolicy"/> over a realistic set of chains and
/// assert on the metadata that gets attached. Stops at "the correct metadata is present"; does not
/// assert how the matcher will later interpret it.
/// </summary>
public class PolicyOutputTests
{
    // OUT-1 — versioning is applied exactly once per versioned chain: the finalizer (fed the single
    // WithApiVersionSet) emits exactly one ApiVersionMetadata.
    [Fact]
    public void OUT1_version_set_applied_exactly_once_per_versioned_chain()
    {
        var chain = VersioningHarness.ChainFor<Out1VersionedHandler>(x => x.Get());

        VersioningHarness.Apply(chain);

        chain.VersionMetadataCount().ShouldBe(1);
    }

    // OUT-2 — per-chain provider roles land in the right ApiVersionModel buckets. Note Asp.Versioning
    // folds advertised versions into SupportedApiVersions (and advertised-deprecated into
    // DeprecatedApiVersions) by design — ApiVersionModel has no separate advertised list — so the
    // advertised role is asserted as membership in the supported/deprecated buckets.
    [Fact]
    public void OUT2_per_chain_provider_roles_are_correct()
    {
        var served = VersioningHarness.ChainFor<Out2ServedHandler>(x => x.Served());
        var deprecated = VersioningHarness.ChainFor<Out2DeprecatedHandler>(x => x.Deprecated());
        var mapped = VersioningHarness.ChainFor<Out2MappedHandler>(x => x.Mapped());
        var advertised = VersioningHarness.ChainFor<Out2AdvertisedHandler>(x => x.Advertised());
        var advertisedDeprecated = VersioningHarness.ChainFor<Out2AdvertisedDeprecatedHandler>(x => x.AdvertisedDeprecated());

        VersioningHarness.Apply(served, deprecated, mapped, advertised, advertisedDeprecated);

        // served → None role: implemented + supported
        var servedModel = served.EndpointModel();
        servedModel.SupportedApiVersions.ShouldContain(new ApiVersion(1, 0));
        servedModel.ImplementedApiVersions.ShouldContain(new ApiVersion(1, 0));

        // served-deprecated → Deprecated role
        deprecated.EndpointModel().DeprecatedApiVersions.ShouldContain(new ApiVersion(2, 0));

        // mapped → Mapped role
        mapped.VersionMetadata()!.IsMappedTo(new ApiVersion(3, 0)).ShouldBeTrue();

        // advertised → folds into the supported bucket (Asp.Versioning's ApiVersionModel design)
        advertised.EndpointModel().SupportedApiVersions.ShouldContain(new ApiVersion(4, 0));

        // advertised-deprecated → folds into the deprecated bucket
        advertisedDeprecated.EndpointModel().DeprecatedApiVersions.ShouldContain(new ApiVersion(5, 0));
    }

    // OUT-3 — a neutral chain has neutral metadata attached (and, by construction, no surviving
    // version set: the finalizer produces ApiVersionMetadata.Neutral).
    [Fact]
    public void OUT3_neutral_chain_has_neutral_metadata_and_no_set()
    {
        var chain = VersioningHarness.ChainFor<Out3NeutralHandler>(x => x.Get());

        VersioningHarness.Apply(chain);

        chain.VersionMetadata()!.IsApiVersionNeutral.ShouldBeTrue();
    }

    // OUT-4 — an unversioned chain is left completely untouched: no version metadata added at all.
    [Fact]
    public void OUT4_unversioned_chain_is_untouched()
    {
        var chain = VersioningHarness.ChainFor<Out4UnversionedHandler>(x => x.Get());

        VersioningHarness.Apply(chain);

        chain.VersionMetadata().ShouldBeNull();
    }

    // OUT-5 — the set is attached before the per-chain provider conventions, so endpoint finalization
    // succeeds and produces a non-neutral ApiVersionMetadata without tripping NoVersionSet. (Ordering
    // itself is exercised end-to-end by E2E-2.)
    [Fact]
    public void OUT5_set_attached_before_per_chain_conventions()
    {
        var chain = VersioningHarness.ChainFor<Out5VersionedHandler>(x => x.Get());

        VersioningHarness.Apply(chain);

        var metadata = chain.VersionMetadata();
        metadata.ShouldNotBeNull();
        metadata!.IsApiVersionNeutral.ShouldBeFalse();
    }
}
