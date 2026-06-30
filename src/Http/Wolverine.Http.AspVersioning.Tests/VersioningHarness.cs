using System.Linq.Expressions;
using System.Text.Json;
using Asp.Versioning;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using JasperFx.CodeGeneration.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Wolverine.Http.AspVersioning.Tests;

/// <summary>
/// Shared helpers for driving <see cref="AspVersioningPolicy"/> over a set of host-free
/// <see cref="HttpChain"/> instances and reading back the Asp.Versioning metadata the policy
/// attaches.
///
/// The policy opts each versioned chain into <c>HttpChain.RequiresApplicationServices</c>, which makes
/// <c>BuildEndpoint</c> hand Asp.Versioning's endpoint finalizer the chain's <c>_parent.Container.Services</c>.
/// So chains must be built against a parent <see cref="HttpGraph"/> whose container has
/// <c>AddApiVersioning()</c> registered — otherwise the finalizer cannot resolve
/// <c>IOptions&lt;ApiVersioningOptions&gt;</c>. Build chains via <see cref="ChainFor{T}"/>.
/// </summary>
internal static class VersioningHarness
{
    // One parent HttpGraph whose container carries the Asp.Versioning services the endpoint finalizer
    // resolves. Mirrors HttpChain.ChainFor's own registrations, plus AddApiVersioning()/AddLogging().
    private static readonly HttpGraph _parent = BuildParent();

    private static HttpGraph BuildParent()
    {
        var registry = new ServiceCollection();
        registry.AddLogging();
        registry.AddApiVersioning();
        registry.AddSingleton<JsonSerializerOptions>();
        registry.AddTransient<IServiceVariableSource, ServiceCollectionServerVariableSource>();
        registry.AddSingleton<IServiceContainer, ServiceContainer>();
        registry.AddSingleton<IServiceCollection>(registry);

        var provider = registry.BuildServiceProvider();
        return new HttpGraph(new WolverineOptions(), provider.GetRequiredService<IServiceContainer>());
    }

    /// <summary>Build a host-free chain whose parent container can satisfy the Asp.Versioning finalizer.</summary>
    public static HttpChain ChainFor<T>(Expression<Action<T>> expression)
        => HttpChain.ChainFor(expression, _parent);

    /// <summary>Run the policy under test over <paramref name="chains"/>.</summary>
    public static void Apply(params HttpChain[] chains)
        => new AspVersioningPolicy().Apply(chains, new GenerationRules(), _parent.Container);

    /// <summary>Materialize the endpoint and read the attached <see cref="ApiVersionMetadata"/> (null when none).</summary>
    public static ApiVersionMetadata? VersionMetadata(this HttpChain chain)
        => chain.BuildEndpoint(RouteWarmup.Lazy).Metadata.GetMetadata<ApiVersionMetadata>();

    /// <summary>How many <see cref="ApiVersionMetadata"/> instances are attached — used to assert "exactly once".</summary>
    public static int VersionMetadataCount(this HttpChain chain)
        => chain.BuildEndpoint(RouteWarmup.Lazy).Metadata.GetOrderedMetadata<ApiVersionMetadata>().Count;

    /// <summary>
    /// The group-wide (set) <see cref="ApiVersionModel"/> — the aggregate version space shared by
    /// every chain in the route group. First component of <see cref="ApiVersionMetadata"/>.
    /// </summary>
    public static ApiVersionModel GroupModel(this HttpChain chain)
    {
        var (api, _) = chain.VersionMetadata()!;
        return api;
    }

    /// <summary>
    /// The per-endpoint <see cref="ApiVersionModel"/> — the versions this specific chain declares,
    /// implements, supports, and deprecates. Second component of <see cref="ApiVersionMetadata"/>.
    /// </summary>
    public static ApiVersionModel EndpointModel(this HttpChain chain)
    {
        var (_, endpoint) = chain.VersionMetadata()!;
        return endpoint;
    }
}
