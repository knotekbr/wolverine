using Alba;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Wolverine.Http.AspVersioning.Tests;

// Public so Wolverine's HTTP discovery (which requires IsPublic) maps them; the internal Tier-1
// fixtures are deliberately invisible to discovery and never reach a host.

public class E2EOrdersV1Endpoint
{
    [WolverineGet("/e2e/orders")]
    [ApiVersion("1.0")]
    public string Get() => "orders-v1";
}

public class E2EOrdersV2Endpoint
{
    [WolverineGet("/e2e/orders")]
    [ApiVersion("2.0")]
    public string Get() => "orders-v2";
}

// Same route "/e2e/conflict": version 1.0 is supported by one chain and deprecated by another
// (supported-wins should put 1.0 in supported only). A third chain serves 3.0 as an unambiguous
// version to request — a 1.0 request would match both conflicting chains and be ambiguous, but the
// reported headers describe the whole shared set regardless of which version was requested.
public class E2EConflictSupportedEndpoint
{
    [WolverineGet("/e2e/conflict")]
    [ApiVersion("1.0")]
    public string Get() => "supported";
}

public class E2EConflictDeprecatedEndpoint
{
    [WolverineGet("/e2e/conflict")]
    [ApiVersion("1.0", Deprecated = true)]
    public string Get() => "deprecated";
}

public class E2EConflictProbeEndpoint
{
    [WolverineGet("/e2e/conflict")]
    [ApiVersion("3.0")]
    public string Get() => "probe";
}

/// <summary>
/// Tier 3 — E2E: a real Wolverine + Asp.Versioning host. Proves the cross-library assumptions that
/// host-free unit tests structurally cannot. The regression canary for dependency upgrades.
/// </summary>
public class EndToEndVersioningTests
{
    private static async Task<IAlbaHost> StartHostAsync()
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());

        builder.Services.AddApiVersioning(options =>
        {
            options.ReportApiVersions = true;
        });

        builder.Host.UseWolverine(opts =>
        {
            opts.Discovery.DisableConventionalDiscovery();
            opts.Discovery.IncludeAssembly(typeof(EndToEndVersioningTests).Assembly);
        });

        builder.Services.AddWolverineHttp();

        return await AlbaHost.For(builder, app =>
        {
            app.MapWolverineEndpoints(opts => opts.UseAspVersioning());
        });
    }

    // E2E-1 (core spike) — directly-attached ApiVersionMetadata is honored by ApiVersionMatcherPolicy:
    // a v1 request reaches the v1 endpoint and a v2 request reaches the v2 endpoint at the same route.
    // If this fails, the whole approach is invalid.
    [Fact]
    public async Task E2E1_requested_version_routes_to_matching_endpoint()
    {
        await using var host = await StartHostAsync();

        var v1 = await host.Scenario(x =>
        {
            x.Get.Url("/e2e/orders?api-version=1.0");
            x.StatusCodeShouldBeOk();
        });
        (await v1.ReadAsTextAsync()).ShouldBe("orders-v1");

        var v2 = await host.Scenario(x =>
        {
            x.Get.Url("/e2e/orders?api-version=2.0");
            x.StatusCodeShouldBeOk();
        });
        (await v2.ReadAsTextAsync()).ShouldBe("orders-v2");
    }

    // E2E-2 — the app starts cleanly with versioned Wolverine endpoints (set-before-conventions
    // ordering does not trip NoVersionSet at host build time).
    [Fact]
    public async Task E2E2_host_starts_cleanly_with_versioned_endpoints()
    {
        await using var host = await StartHostAsync();
        host.ShouldNotBeNull();
    }

    // E2E-3 — a Wolverine endpoint is not treated as "grouped", so WithApiVersionSet does not throw
    // MultipleVersionSets even with AddApiVersioning registered in the container.
    [Fact]
    public async Task E2E3_versioned_endpoint_does_not_throw_multiple_version_sets()
    {
        await Should.NotThrowAsync(async () =>
        {
            await using var host = await StartHostAsync();
        });
    }

    // E2E-4 — a version supported by one chain and deprecated by another (supported-wins) is reported
    // in api-supported-versions and NOT in api-deprecated-versions (requires ReportApiVersions). Proves
    // the dedup actually prevents the contradictory-headers outcome ApiVersionModel would otherwise
    // permit, and that the finalizer's header-reporting decorator shipped end-to-end. Requests the
    // unambiguous 3.0 sibling; the headers describe the whole shared set.
    [Fact]
    public async Task E2E4_conflicting_version_reported_only_as_supported()
    {
        await using var host = await StartHostAsync();

        var result = await host.Scenario(x =>
        {
            x.Get.Url("/e2e/conflict?api-version=3.0");
            x.StatusCodeShouldBeOk();
        });

        var supported = result.Context.Response.Headers["api-supported-versions"].ToString();
        var deprecated = result.Context.Response.Headers["api-deprecated-versions"].ToString();

        supported.ShouldContain("1.0");
        deprecated.ShouldNotContain("1.0");
    }
}
