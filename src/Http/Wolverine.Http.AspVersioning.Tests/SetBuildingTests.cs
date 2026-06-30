using Asp.Versioning;
using Shouldly;

namespace Wolverine.Http.AspVersioning.Tests;

// ---------- Fixture handlers ----------

[ApiVersion("1.0")]
[ApiVersion("2.0", Deprecated = true)]
internal class Set1MixedHandler
{
    [WolverineGet("/set1")]
    public string Get() => "mixed";
}

[ApiVersion("1.0")]
internal class Set2SupportedHandler
{
    [WolverineGet("/set2")]
    public string Get() => "supported";
}

[ApiVersion("1.0", Deprecated = true)]
internal class Set2DeprecatedHandler
{
    [WolverineGet("/set2")]
    public string Get() => "deprecated";
}

[AdvertiseApiVersions("3.0")]
[AdvertiseApiVersions("4.0", Deprecated = true)]
internal class Set3AdvertisedHandler
{
    [WolverineGet("/set3")]
    public string Get() => "advertised";
}

[ApiVersion("1.0")]
internal class Set4FirstHandler
{
    [WolverineGet("/set4")]
    public string Get() => "first";
}

[ApiVersion("2.0")]
internal class Set4SecondHandler
{
    [WolverineGet("/set4")]
    public string Get() => "second";
}

[ApiVersionNeutral]
internal class Set5FirstNeutralHandler
{
    [WolverineGet("/set5")]
    public string Get() => "neutral-1";
}

[ApiVersionNeutral]
internal class Set5SecondNeutralHandler
{
    [WolverinePost("/set5")]
    public string Post() => "neutral-2";
}

internal class Set6UnversionedHandler
{
    [WolverineGet("/set6")]
    public string Get() => "unversioned";
}

[ApiVersion("1.0")]
internal class Set7FirstHandler
{
    [WolverineGet("/set7")]
    public string Get() => "first";
}

[ApiVersion("1.0")]
internal class Set7SecondHandler
{
    [WolverineGet("/set7")]
    public string Get() => "second";
}

// ---------- Tests ----------

/// <summary>
/// Tier 1 — SET: bucketing into supported/deprecated and the construction of the per-group
/// <see cref="Asp.Versioning.Builder.ApiVersionSet"/>. The single-chain bucketing decisions live in
/// <see cref="VersionedChain"/> (host-free); the cross-chain set decisions live in
/// <see cref="AspVersioningPolicy"/> and are observed through the attached set/metadata.
/// </summary>
public class SetBuildingTests
{
    // SET-1 — a chain serving 1.0 and deprecated-2.0 → correct two-bucket partition on the
    // VersionedChain (supported vs deprecated).
    [Fact]
    public void SET1_supported_and_deprecated_partition_correctly()
    {
        var chain = VersioningHarness.ChainFor<Set1MixedHandler>(x => x.Get());

        var vc = VersionedChain.FromHttpChain(chain);
        vc.Supported.Select(r => r.Version).ShouldBe(new[] { new ApiVersion(1, 0) });
        vc.Deprecated.Select(r => r.Version).ShouldBe(new[] { new ApiVersion(2, 0) });
    }

    // SET-2 — same version supported by one chain and deprecated by another → supported wins; 1.0
    // appears only in the supported bucket. This matches Asp.Versioning's own merge rule
    // (ApiVersionModelExtensions.Aggregate: deprecated.ExceptWith(supported)) and resolves the
    // contradiction ApiVersionModel would otherwise leave (the version in both buckets).
    [Fact]
    public void SET2_supported_wins_over_deprecated_for_same_version()
    {
        var supported = VersioningHarness.ChainFor<Set2SupportedHandler>(x => x.Get());
        var deprecated = VersioningHarness.ChainFor<Set2DeprecatedHandler>(x => x.Get());

        VersioningHarness.Apply(supported, deprecated);

        var group = supported.GroupModel();
        group.SupportedApiVersions.ShouldContain(new ApiVersion(1, 0));
        group.DeprecatedApiVersions.ShouldNotContain(new ApiVersion(1, 0));
    }

    // SET-3 — advertised folds into the supported bucket and advertised-deprecated into the
    // deprecated bucket (so they surface in the api-supported/deprecated-versions headers). In
    // Asp.Versioning v10 the read-side ApiVersionModel exposes no separate advertised list — it
    // folds advertised into Supported/Deprecated at construction — so the one observable signal that
    // 3.0/4.0 were emitted as ADVERTISED (AdvertisesApiVersion) rather than declared (HasApiVersion)
    // is their ABSENCE from DeclaredApiVersions (declared = supported ∪ deprecated, never advertised).
    [Fact]
    public void SET3_advertised_folds_into_supported_and_deprecated_buckets()
    {
        var chain = VersioningHarness.ChainFor<Set3AdvertisedHandler>(x => x.Get());

        VersioningHarness.Apply(chain);

        var group = chain.GroupModel();
        group.SupportedApiVersions.ShouldContain(new ApiVersion(3, 0));
        group.DeprecatedApiVersions.ShouldContain(new ApiVersion(4, 0));
        group.DeclaredApiVersions.ShouldNotContain(new ApiVersion(3, 0));
        group.DeclaredApiVersions.ShouldNotContain(new ApiVersion(4, 0));
    }

    // SET-4 — exactly one ApiVersionSet is shared across all chains in a group. The finalizer consumes
    // that single set into each chain's ApiVersionMetadata, so the decision is observed as an identical
    // aggregate (group-wide) version space across every chain in the group.
    [Fact]
    public void SET4_single_set_instance_shared_across_group()
    {
        var first = VersioningHarness.ChainFor<Set4FirstHandler>(x => x.Get());
        var second = VersioningHarness.ChainFor<Set4SecondHandler>(x => x.Get());

        VersioningHarness.Apply(first, second);

        var union = new[] { new ApiVersion(1, 0), new ApiVersion(2, 0) };
        first.GroupModel().SupportedApiVersions.ShouldBe(union, ignoreOrder: true);
        second.GroupModel().SupportedApiVersions.ShouldBe(union, ignoreOrder: true);
    }

    // SET-5 — an all-neutral group → no version set is built; each chain receives neutral metadata.
    // (The set is consumed by the finalizer, so neutrality is the observable signal.)
    [Fact]
    public void SET5_all_neutral_group_builds_no_set()
    {
        var first = VersioningHarness.ChainFor<Set5FirstNeutralHandler>(x => x.Get());
        var second = VersioningHarness.ChainFor<Set5SecondNeutralHandler>(x => x.Post());

        VersioningHarness.Apply(first, second);

        first.VersionMetadata()!.IsApiVersionNeutral.ShouldBeTrue();
        second.VersionMetadata()!.IsApiVersionNeutral.ShouldBeTrue();
    }

    // SET-6 — an empty/unversioned group → no version metadata. An unversioned chain is filtered out
    // before the policy attaches anything, so the endpoint builds without any versioning metadata.
    [Fact]
    public void SET6_unversioned_group_builds_no_set_and_no_metadata()
    {
        var chain = VersioningHarness.ChainFor<Set6UnversionedHandler>(x => x.Get());

        VersioningHarness.Apply(chain);

        chain.VersionMetadata().ShouldBeNull();
    }

    // SET-7 — duplicate identical contributions (same version, same deprecation state, from
    // multiple chains) collapse to a single entry; no exception. Confirms duplicates are harmless,
    // so the fold is only required for the conflict case (SET-2), not for plain dedup.
    [Fact]
    public void SET7_duplicate_identical_contributions_collapse_without_error()
    {
        var first = VersioningHarness.ChainFor<Set7FirstHandler>(x => x.Get());
        var second = VersioningHarness.ChainFor<Set7SecondHandler>(x => x.Get());

        Should.NotThrow(() => VersioningHarness.Apply(first, second));

        first.GroupModel().SupportedApiVersions.ShouldBe(new[] { new ApiVersion(1, 0) });
    }
}
