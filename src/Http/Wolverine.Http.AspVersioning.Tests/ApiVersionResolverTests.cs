using System.Reflection;
using Asp.Versioning;
using Shouldly;
using Wolverine.Http.ApiVersioning;

namespace Wolverine.Http.AspVersioning.Tests;

// ---------- Fixture handlers (attribute permutations the resolver must disambiguate) ----------

[ApiVersion("1.0")]
internal class Res1MethodOverridesClassHandler
{
    [ApiVersion("2.0")]
    public void Handle() { }
}

[ApiVersion("1.0")]
[ApiVersion("2.0")]
internal class Res2MapToFiltersClassHandler
{
    [MapToApiVersion("1.0")]
    public void Handle() { }
}

[ApiVersion("1.0", Deprecated = true)]
internal class Res3MapToCarriesClassDeprecationHandler
{
    [MapToApiVersion("1.0")]
    public void Handle() { }
}

[ApiVersionNeutral]
internal class Res4MethodVersionBeatsClassNeutralHandler
{
    [ApiVersion("2.0")]
    public void Handle() { }
}

[ApiVersion("1.0")]
[ApiVersion("2.0")]
internal class Res5ClassOnlyHandler
{
    public void Handle() { }
}

internal class Res6NoAttributesHandler
{
    [WolverineGet("/res6")]
    public string Get() => "none";
}

[ApiVersion("1.0", Deprecated = true)]
internal class Res7ServedAndMappedHandler
{
    [MapToApiVersion("1.0")]
    public void Handle() { }
}

internal class Res8ApiVersionAndMapToHandler
{
    [ApiVersion("2.0")]
    [MapToApiVersion("2.0")]
    public void Handle() { }
}

internal class Res9MapToWithoutClassApiVersionHandler
{
    [MapToApiVersion("1.0")]
    public void Handle() { }
}

internal class Res10MethodConflictHandler
{
    [ApiVersion("1.0")]
    [ApiVersionNeutral]
    public void Handle() { }
}

[ApiVersion("1.0")]
[ApiVersionNeutral]
internal class Res10ClassConflictHandler
{
    public void Handle() { }
}

[ApiVersion("1.0")]
internal class Res11MapToOutsideClassSubsetHandler
{
    [MapToApiVersion("3.0")]
    public void Handle() { }
}

// ---------- Tests ----------

/// <summary>
/// Tier 1 — RES: resolution &amp; precedence, driven directly against the resolvers the
/// AspVersioning integration consumes via <see cref="VersionedChain.FromHttpChain"/>
/// (<see cref="ApiVersionResolver.ResolveVersions"/> and <see cref="ApiVersionNeutralResolver.Resolve"/>).
/// </summary>
public class ApiVersionResolverTests
{
    private static MethodInfo MethodOf<T>(string name = "Handle")
        => typeof(T).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!;

    // RES-1 — method [ApiVersion("2.0")] + class [ApiVersion("1.0")] → {2.0} only.
    // Method REPLACES class; explicitly NOT the {1.0, 2.0} union Asp.Versioning would otherwise produce.
    [Fact]
    public void RES1_method_apiversion_replaces_class_apiversion()
    {
        var result = ApiVersionResolver.ResolveVersions(MethodOf<Res1MethodOverridesClassHandler>());

        result.Select(r => r.Version).ShouldBe(new[] { new ApiVersion(2, 0) });
        result.Select(r => r.Version).ShouldNotContain(new ApiVersion(1, 0));
    }

    // RES-2 — method [MapToApiVersion("1.0")] + class [ApiVersion("1.0")],[ApiVersion("2.0")] → {1.0};
    // deprecation is sourced from the class attributes.
    [Fact]
    public void RES2_mapto_filters_class_versions_to_subset()
    {
        var result = ApiVersionResolver.ResolveVersions(MethodOf<Res2MapToFiltersClassHandler>());

        result.Select(r => r.Version).ShouldBe(new[] { new ApiVersion(1, 0) });
        result.Single().IsDeprecated.ShouldBeFalse();
    }

    // RES-3 — class [ApiVersion("1.0", Deprecated = true)] + method [MapToApiVersion("1.0")] →
    // 1.0 carries the class-sourced Deprecated option.
    [Fact]
    public void RES3_mapto_inherits_class_deprecation()
    {
        var result = ApiVersionResolver.ResolveVersions(MethodOf<Res3MapToCarriesClassDeprecationHandler>());

        result.Single().Version.ShouldBe(new ApiVersion(1, 0));
        result.Single().IsDeprecated.ShouldBeTrue();
    }

    // RES-4 — method [ApiVersion("2.0")] + class [ApiVersionNeutral] → versioned {2.0}, NOT neutral.
    [Fact]
    public void RES4_method_apiversion_overrides_class_neutral()
    {
        var method = MethodOf<Res4MethodVersionBeatsClassNeutralHandler>();

        ApiVersionResolver.ResolveVersions(method).Select(r => r.Version)
            .ShouldBe(new[] { new ApiVersion(2, 0) });
        ApiVersionNeutralResolver.Resolve(method).ShouldBeFalse();
    }

    // RES-5 — class [ApiVersion] only → resolved equals the class versions.
    [Fact]
    public void RES5_class_only_resolves_to_class_versions()
    {
        var result = ApiVersionResolver.ResolveVersions(MethodOf<Res5ClassOnlyHandler>());

        result.Select(r => r.Version).ShouldBe(new[] { new ApiVersion(1, 0), new ApiVersion(2, 0) });
    }

    // RES-6 — no version attributes anywhere → resolver yields empty AND the chain is excluded
    // from versioning entirely (VersionedChain.HasVersioningInfo is false).
    [Fact]
    public void RES6_no_attributes_yields_empty_and_excludes_chain()
    {
        var chain = HttpChain.ChainFor<Res6NoAttributesHandler>(x => x.Get());

        ApiVersionResolver.ResolveVersions(chain.Method.Method).ShouldBeEmpty();
        VersionedChain.FromHttpChain(chain).HasVersioningInfo.ShouldBeFalse();
    }

    // RES-7 — a version that is both served and mapped → options accumulate: the Mapped bit is
    // present and (here) deprecation sets the Deprecated bit too.
    [Fact]
    public void RES7_served_and_mapped_accumulates_provider_options()
    {
        var result = ApiVersionResolver.ResolveVersions(MethodOf<Res7ServedAndMappedHandler>());

        var resolution = result.Single();
        resolution.Version.ShouldBe(new ApiVersion(1, 0));
        resolution.Options.HasFlag(ApiVersionProviderOptions.Mapped).ShouldBeTrue();
        resolution.Options.HasFlag(ApiVersionProviderOptions.Deprecated).ShouldBeTrue();
    }

    // RES-8 (guard) — method declares both [ApiVersion] and [MapToApiVersion] → throws; the
    // message names the offending method.
    [Fact]
    public void RES8_apiversion_and_mapto_on_same_method_throws()
    {
        var ex = Should.Throw<InvalidOperationException>(
            () => ApiVersionResolver.ResolveVersions(MethodOf<Res8ApiVersionAndMapToHandler>()));

        ex.Message.ShouldContain(nameof(Res8ApiVersionAndMapToHandler));
        ex.Message.ShouldContain(nameof(Res8ApiVersionAndMapToHandler.Handle));
    }

    // RES-9 (guard) — method has [MapToApiVersion] but the class has no [ApiVersion] → throws;
    // the message names both the method and the class.
    [Fact]
    public void RES9_mapto_without_class_apiversion_throws()
    {
        var ex = Should.Throw<InvalidOperationException>(
            () => ApiVersionResolver.ResolveVersions(MethodOf<Res9MapToWithoutClassApiVersionHandler>()));

        ex.Message.ShouldContain(nameof(Res9MapToWithoutClassApiVersionHandler));
        ex.Message.ShouldContain(nameof(Res9MapToWithoutClassApiVersionHandler.Handle));
    }

    // RES-10 (guard) — [ApiVersion] + [ApiVersionNeutral] on the same target throws, both at the
    // method level and at the class level; the message names the attributes and the offending member.
    [Fact]
    public void RES10_apiversion_and_neutral_on_method_throws()
    {
        var ex = Should.Throw<InvalidOperationException>(
            () => ApiVersionNeutralResolver.Resolve(MethodOf<Res10MethodConflictHandler>()));

        ex.Message.ShouldContain(nameof(Res10MethodConflictHandler));
        ex.Message.ShouldContain(nameof(Res10MethodConflictHandler.Handle));
        ex.Message.ShouldContain(ApiVersionNeutralResolver.ApiVersionAttributeName);
        ex.Message.ShouldContain(ApiVersionNeutralResolver.ApiVersionNeutralAttributeName);
    }

    [Fact]
    public void RES10_apiversion_and_neutral_on_class_throws()
    {
        var ex = Should.Throw<InvalidOperationException>(
            () => ApiVersionNeutralResolver.Resolve(MethodOf<Res10ClassConflictHandler>()));

        ex.Message.ShouldContain(nameof(Res10ClassConflictHandler));
        ex.Message.ShouldContain(ApiVersionNeutralResolver.ApiVersionAttributeName);
        ex.Message.ShouldContain(ApiVersionNeutralResolver.ApiVersionNeutralAttributeName);
    }

    // RES-11 (guard) — [MapToApiVersion] lists a version not declared on the class → throws
    // (subset violation); the message names the method and the class.
    [Fact]
    public void RES11_mapto_version_outside_class_subset_throws()
    {
        var ex = Should.Throw<InvalidOperationException>(
            () => ApiVersionResolver.ResolveVersions(MethodOf<Res11MapToOutsideClassSubsetHandler>()));

        ex.Message.ShouldContain(nameof(Res11MapToOutsideClassSubsetHandler));
        ex.Message.ShouldContain(nameof(Res11MapToOutsideClassSubsetHandler.Handle));
    }
}
