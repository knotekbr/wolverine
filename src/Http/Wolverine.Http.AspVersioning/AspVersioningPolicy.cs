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
            vc.Chain.WithApiVersionSet(versionSet);

            if (vc.IsVersionNeutral)
            {
                vc.Chain.IsApiVersionNeutral();
                continue;
            }

            foreach (var supported in vc.Supported)
            {
                if (supported.Options.HasFlag(ApiVersionProviderOptions.Mapped))
                    vc.Chain.MapToApiVersion(supported.Version);
                else
                    vc.Chain.HasApiVersion(supported.Version);
            }

            foreach (var deprecated in vc.Deprecated)
                vc.Chain.HasDeprecatedApiVersion(deprecated.Version);

            foreach (var advertised in vc.Advertised)
                vc.Chain.AdvertisesApiVersion(advertised.Version);

            foreach (var advertisedDeprecated in vc.AdvertisedDeprecated)
                vc.Chain.AdvertisesDeprecatedApiVersion(advertisedDeprecated.Version);
        }
    }

    private static ApiVersionSet buildVersionSet(IReadOnlyList<VersionedChain> versionedChains)
    {
        var versionSetBuilder = new ApiVersionSetBuilder(null);

        foreach (var supported in versionedChains.SelectMany(vc => vc.AllSupported))
            versionSetBuilder.HasApiVersion(supported.Version);

        foreach (var deprecated in versionedChains.SelectMany(vc => vc.AllDeprecated))
            versionSetBuilder.HasDeprecatedApiVersion(deprecated.Version);

        return versionSetBuilder.Build();
    }

    private static string normalizeRoute(string? rawRoutePattern) =>
        string.IsNullOrWhiteSpace(rawRoutePattern) ? string.Empty : rawRoutePattern.Trim('/');
}
