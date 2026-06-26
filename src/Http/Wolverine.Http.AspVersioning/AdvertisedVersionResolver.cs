using System.Reflection;
using Asp.Versioning;
using Wolverine.Http.ApiVersioning;

namespace Wolverine.Http.AspVersioning;

internal static class AdvertisedVersionResolver
{
    public static IReadOnlyList<ApiVersionResolution> ResolveAdvertised(MethodInfo method)
    {
        return getMergedAdvertiseAttributes(method)
            .Cast<IApiVersionProvider>()
            .SelectMany(
                provider => provider.Versions,
                (provider, version) => (Version: version, provider.Options)
            )
            .Aggregate(
                new Dictionary<ApiVersion, ApiVersionProviderOptions>(),
                (dict, tuple) =>
                {
                    if (!dict.TryGetValue(tuple.Version, out var options))
                        options = tuple.Options;

                    dict[tuple.Version] = options | tuple.Options;
                    return dict;
                }
            )
            .Select(kvp => new ApiVersionResolution(kvp.Key, kvp.Value))
            .ToList();
    }

    private static IEnumerable<AdvertiseApiVersionsAttribute> getMergedAdvertiseAttributes(
        MethodInfo method
    ) =>
        method
            .GetCustomAttributes<AdvertiseApiVersionsAttribute>(inherit: false)
            .Concat(
                method.DeclaringType?.GetCustomAttributes<AdvertiseApiVersionsAttribute>(
                    inherit: false
                ) ?? []
            );
}
