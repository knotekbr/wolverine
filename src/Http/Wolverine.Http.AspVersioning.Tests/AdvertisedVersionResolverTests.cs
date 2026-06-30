using System.Reflection;
using Asp.Versioning;
using Shouldly;
using Wolverine.Http.ApiVersioning;

namespace Wolverine.Http.AspVersioning.Tests;

// ---------- Fixture handlers ----------

[AdvertiseApiVersions("4.0")]
internal class Adv1MethodAndClassHandler
{
    [AdvertiseApiVersions("3.0")]
    public void Handle() { }
}

[AdvertiseApiVersions("3.0")]
internal class Adv2DuplicateHandler
{
    [AdvertiseApiVersions("3.0")]
    public void Handle() { }
}

[AdvertiseApiVersions("3.0", Deprecated = true)]
internal class Adv3DeprecationOrsHandler
{
    [AdvertiseApiVersions("3.0")]
    public void Handle() { }
}

// ADV-4 — two chains sharing route "/adv4": one advertises 2.0, the other implements it.
[ApiVersion("1.0")]
[AdvertiseApiVersions("2.0")]
internal class Adv4AdvertiserHandler
{
    [WolverineGet("/adv4")]
    public string Get() => "advertiser";
}

[ApiVersion("2.0")]
internal class Adv4ImplementerHandler
{
    [WolverineGet("/adv4")]
    public string Get() => "implementer";
}

// ADV-5 — advertises 3.0 only; implemented by no chain.
[AdvertiseApiVersions("3.0")]
internal class Adv5OnlyAdvertisesHandler
{
    [WolverineGet("/adv5")]
    public string Get() => "advertised-only";
}

// ---------- Tests ----------

/// <summary>
/// Tier 1 — ADV: the advertised-version merge owned by
/// <see cref="AdvertisedVersionResolver.ResolveAdvertised"/> (ADV-1..3), plus the group-level
/// fold of advertised versions performed by <see cref="AspVersioningPolicy"/> (ADV-4..5).
/// </summary>
public class AdvertisedVersionResolverTests
{
    private static MethodInfo MethodOf<T>(string name = "Handle")
        => typeof(T).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!;

    // ADV-1 — method advertises 3.0, class advertises 4.0 → union {3.0, 4.0}, no precedence.
    [Fact]
    public void ADV1_method_and_class_advertised_versions_union()
    {
        var result = AdvertisedVersionResolver.ResolveAdvertised(MethodOf<Adv1MethodAndClassHandler>());

        result.Select(r => r.Version).OrderBy(v => v)
            .ShouldBe(new[] { new ApiVersion(3, 0), new ApiVersion(4, 0) });
    }

    // ADV-2 — same advertised version on both method and class → appears exactly once.
    [Fact]
    public void ADV2_duplicate_advertised_version_appears_once()
    {
        var result = AdvertisedVersionResolver.ResolveAdvertised(MethodOf<Adv2DuplicateHandler>());

        result.Count(r => r.Version == new ApiVersion(3, 0)).ShouldBe(1);
    }

    // ADV-3 — class advertises 3.0 deprecated, method advertises 3.0 current → deprecation ORs
    // across sources, so 3.0 is advertised-deprecated.
    [Fact]
    public void ADV3_deprecation_ors_across_sources()
    {
        var result = AdvertisedVersionResolver.ResolveAdvertised(MethodOf<Adv3DeprecationOrsHandler>());

        result.Single().Version.ShouldBe(new ApiVersion(3, 0));
        result.Single().IsDeprecated.ShouldBeTrue();
    }

    // ADV-4 — a version advertised by one chain but implemented by another in the same group
    // resolves to implemented; the group's version space lists 2.0 as a supported (non-deprecated)
    // version and the implementer's endpoint actually implements it.
    [Fact]
    public void ADV4_implemented_beats_advertised_within_group()
    {
        var advertiser = VersioningHarness.ChainFor<Adv4AdvertiserHandler>(x => x.Get());
        var implementer = VersioningHarness.ChainFor<Adv4ImplementerHandler>(x => x.Get());

        VersioningHarness.Apply(advertiser, implementer);

        var group = advertiser.GroupModel();
        group.SupportedApiVersions.ShouldContain(new ApiVersion(2, 0));
        group.DeprecatedApiVersions.ShouldNotContain(new ApiVersion(2, 0));

        implementer.EndpointModel().ImplementedApiVersions.ShouldContain(new ApiVersion(2, 0));
    }

    // ADV-5 — a version advertised only (implemented nowhere) folds into the supported bucket at the
    // set level but is emitted as advertised, not declared: it appears in SupportedApiVersions yet is
    // absent from DeclaredApiVersions (the only read-side signal of the advertised role in v10; see
    // SET-3).
    [Fact]
    public void ADV5_advertised_only_folds_into_supported_bucket()
    {
        var chain = VersioningHarness.ChainFor<Adv5OnlyAdvertisesHandler>(x => x.Get());

        var vc = VersionedChain.FromHttpChain(chain);
        vc.AllSupported.Select(r => r.Version).ShouldContain(new ApiVersion(3, 0));

        VersioningHarness.Apply(chain);
        var group = chain.GroupModel();
        group.SupportedApiVersions.ShouldContain(new ApiVersion(3, 0));
        group.DeclaredApiVersions.ShouldNotContain(new ApiVersion(3, 0));
    }
}
