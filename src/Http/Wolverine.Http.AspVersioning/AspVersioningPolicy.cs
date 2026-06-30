using Asp.Versioning;
using Asp.Versioning.Builder;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Builder;

namespace Wolverine.Http.AspVersioning;

internal sealed class AspVersioningPolicy : IHttpPolicy
{
    public void Apply(
        IReadOnlyList<HttpChain> chains,
        GenerationRules rules,
        IServiceContainer container
    )
    {
        var withVersioningInfo = chains
            .Select(VersionedChain.FromHttpChain)
            .Where(c => c.HasVersioningInfo)
            .GroupBy(
                c => normalizeRoute(c.Chain.RoutePattern?.RawText),
                StringComparer.OrdinalIgnoreCase
            );

        foreach (var group in withVersioningInfo)
            applyVersioningMetadata(group.ToList());
    }

    private static void applyVersioningMetadata(IReadOnlyList<VersionedChain> versionedChains)
    {
        var versionSet = buildVersionSet(versionedChains);

        foreach (var vc in versionedChains)
        {
            vc.Chain.RequiresApplicationServices = true;

            if (!vc.Chain.HasExplicitOperationId)
                vc.Chain.SetExplicitOperationId(vc.Chain.OperationId);

            vc.Chain.WithApiVersionSet(versionSet);

            if (vc.IsVersionNeutral)
            {
                vc.Chain.IsApiVersionNeutral();
                continue;
            }

            // Map this chain to every version it serves. Supported-vs-deprecated and advertised are
            // all properties of the shared set built above — MapToApiVersion inherits roles from the
            // set and (unlike HasApiVersion/HasDeprecatedApiVersion) does not mutate it, so there is
            // no per-chain role reconciliation. Advertise-only chains serve nothing, emit nothing,
            // and inherit the set's full space (incl. the advertised fold).
            foreach (var served in vc.Supported.Concat(vc.Deprecated))
                vc.Chain.MapToApiVersion(served.Version);
        }
    }

    private static ApiVersionSet buildVersionSet(IReadOnlyList<VersionedChain> versionedChains)
    {
        var versionSetBuilder = new ApiVersionSetBuilder(null);

        var supported = versionedChains.SelectMany(vc => vc.Supported).Select(r => r.Version).ToHashSet();
        var deprecated = versionedChains.SelectMany(vc => vc.Deprecated).Select(r => r.Version).ToHashSet();
        var advertised = versionedChains.SelectMany(vc => vc.Advertised).Select(r => r.Version).ToHashSet();
        var advertisedDeprecated = versionedChains.SelectMany(vc => vc.AdvertisedDeprecated).Select(r => r.Version).ToHashSet();

        // Supported wins over deprecated for the same version anywhere in the group — applied ONCE,
        // here. A version supported (served or advertised) by any sibling must not also be reported
        // deprecated. This matches Asp.Versioning's own ApiVersionModelExtensions.Aggregate
        // (deprecated.ExceptWith(supported)); the endpoint finalizer never calls Aggregate, and
        // neither ApiVersionSetBuilder nor the ApiVersionModel ctor de-dups, so the policy must seed
        // an already-consistent set.
        var allSupported = new HashSet<ApiVersion>(supported);
        allSupported.UnionWith(advertised);
        deprecated.ExceptWith(allSupported);
        advertisedDeprecated.ExceptWith(allSupported);

        // Seed each version under its role so the set model keeps the advertised lists distinct
        // (AdvertisedApiVersions / DeprecatedAdvertisedApiVersions) while still folding them into
        // SupportedApiVersions / DeprecatedApiVersions for the response headers.
        foreach (var version in supported)
            versionSetBuilder.HasApiVersion(version);

        foreach (var version in deprecated)
            versionSetBuilder.HasDeprecatedApiVersion(version);

        foreach (var version in advertised)
            versionSetBuilder.AdvertisesApiVersion(version);

        foreach (var version in advertisedDeprecated)
            versionSetBuilder.AdvertisesDeprecatedApiVersion(version);

        return versionSetBuilder.Build();
    }

    private static string normalizeRoute(string? rawRoutePattern) =>
        string.IsNullOrWhiteSpace(rawRoutePattern) ? string.Empty : rawRoutePattern.Trim('/');
}
