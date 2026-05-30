using System.Globalization;
using Asp.Versioning;

namespace Wolverine.Http.ApiVersioning;

/// <summary>
/// Configuration options for Wolverine's native API versioning support. Pass an instance to
/// <see cref="WolverineHttpOptions.UseApiVersioning"/> to configure URL-segment behaviour,
/// unversioned-endpoint policy, and per-version sunset / deprecation policies.
/// </summary>
public sealed class WolverineApiVersioningOptions
{
    /// <summary>
    /// URL-segment template injected ahead of versioned routes. The literal <c>{version}</c> is
    /// replaced with the formatted version string produced by <see cref="UrlSegmentVersionFormatter"/>.
    /// Set to <see langword="null"/> to disable URL-segment versioning.
    /// </summary>
    /// <remarks>
    /// Must contain the literal '{version}' token when non-null. Setting a prefix without
    /// the token causes <see cref="ApiVersioningPolicy"/> to throw at startup. Set to null
    /// to disable URL-segment versioning entirely.
    /// </remarks>
    public string? UrlSegmentPrefix { get; set; } = "v{version}";

    /// <summary>
    /// Formatter producing the version string substituted into <see cref="UrlSegmentPrefix"/>.
    /// Defaults to major-only (e.g. <c>"1"</c> for <c>ApiVersion(1, 0)</c> rather than <c>"1.0"</c>).
    /// </summary>
    /// <remarks>
    /// Date-based versions (where <see cref="ApiVersion.MajorVersion"/> is null) fall back to
    /// <see cref="ApiVersion.ToString()"/> which may include hyphens. Override this formatter
    /// if your URL scheme requires a different shape for date-based versions.
    /// </remarks>
    public Func<ApiVersion, string> UrlSegmentVersionFormatter { get; set; }
        = static v => v.MajorVersion?.ToString(CultureInfo.InvariantCulture) ?? v.ToString();

    /// <summary>
    /// Behaviour for endpoints that do not declare an <c>[ApiVersion]</c> attribute.
    /// Defaults to <see cref="UnversionedPolicy.PassThrough"/>.
    /// </summary>
    public UnversionedPolicy UnversionedPolicy { get; set; } = UnversionedPolicy.PassThrough;

    /// <summary>
    /// Used when one or more of the following conditions are met:
    /// <list type="bullet">
    ///   <item>
    ///   <see cref="UnversionedPolicy"/> is <see cref="UnversionedPolicy.AssignDefault"/> (a value is
    ///   required in this case)
    ///   </item>
    ///   <item>
    ///   At least one non-URL version reader is configured, and
    ///   <see cref="AssumeDefaultVersionWhenUnspecified"/> is <see langword="true"/>
    ///   </item>
    /// </list>
    /// Otherwise, this value is ignored.
    /// </summary>
    public ApiVersion? DefaultVersion { get; set; }

    /// <summary>
    /// Emit the <c>api-supported-versions</c> response header on every versioned endpoint.
    /// </summary>
    public bool EmitApiSupportedVersionsHeader { get; set; } = true;

    /// <summary>
    /// Emit RFC 9745 <c>Deprecation</c> and RFC 8594 <c>Sunset</c>/<c>Link</c> headers on endpoints
    /// that have a configured policy.
    /// </summary>
    public bool EmitDeprecationHeaders { get; set; } = true;

    /// <summary>OpenAPI integration options.</summary>
    public WolverineApiVersioningOpenApiOptions OpenApi { get; } = new();

    /// <summary>
    /// The list of <see cref="WolverineApiVersionReader"/> instances used to read API versions from
    /// requests. URL segment versioning is used by default when this list is empty, and is disabled
    /// if any readers are manually added.
    /// </summary>
    public List<WolverineApiVersionReader> VersionReaders { get; set; } = [];

    /// <summary>
    /// Use the version specified by <see cref="DefaultVersion"/> when handling requests that don't
    /// specify an API version.
    /// </summary>
    /// <remarks>
    /// Has no effect unless at least one non-URL version reader is configured.
    /// </remarks>
    public bool AssumeDefaultVersionWhenUnspecified { get; set; } = false;

    /// <summary>
    /// The HTTP status code to return when a request specifies an unsupported API version.
    /// </summary>
    public int UnsupportedApiVersionStatusCode { get; set; } = 400;

    /// <summary>
    /// Per-version sunset policies. Populated via <see cref="Sunset(ApiVersion)"/> or
    /// <see cref="Sunset(string)"/>.
    /// </summary>
    internal Dictionary<ApiVersion, SunsetPolicy> SunsetPolicies { get; } = new();

    /// <summary>
    /// Per-version deprecation policies. Populated via <see cref="Deprecate(ApiVersion)"/> or
    /// <see cref="Deprecate(string)"/>.
    /// </summary>
    internal Dictionary<ApiVersion, DeprecationPolicy> DeprecationPolicies { get; } = new();

    /// <summary>
    /// <see langword="true"/> if any non-URL version readers are configured.
    /// </summary>
    internal bool UsesNonUrlReader => VersionReaders.Count > 0;

    /// <summary>Configure a sunset policy for the given version.</summary>
    /// <param name="version">The API version to configure a sunset policy for.</param>
    /// <returns>A builder that can be used to set dates and link references.</returns>
    public IWolverineSunsetPolicyBuilder Sunset(ApiVersion version) => new SunsetPolicyBuilder(this, version);

    /// <summary>Convenience overload that parses the version string (e.g. <c>"1.0"</c>).</summary>
    /// <param name="version">A version string such as <c>"1.0"</c> or <c>"2"</c>.</param>
    /// <returns>A builder that can be used to set dates and link references.</returns>
    public IWolverineSunsetPolicyBuilder Sunset(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return Sunset(ApiVersionParser.Default.Parse(version));
    }

    /// <summary>Configure a deprecation policy for the given version.</summary>
    /// <param name="version">The API version to configure a deprecation policy for.</param>
    /// <returns>A builder that can be used to set dates and link references.</returns>
    public IWolverineDeprecationPolicyBuilder Deprecate(ApiVersion version) => new DeprecationPolicyBuilder(this, version);

    /// <summary>Convenience overload that parses the version string (e.g. <c>"1.0"</c>).</summary>
    /// <param name="version">A version string such as <c>"1.0"</c> or <c>"2"</c>.</param>
    /// <returns>A builder that can be used to set dates and link references.</returns>
    public IWolverineDeprecationPolicyBuilder Deprecate(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return Deprecate(ApiVersionParser.Default.Parse(version));
    }

    /// <summary>
    /// Configures API versions to be read from a specified HTTP header.
    /// </summary>
    /// <param name="headerName">
    /// The name of the HTTP header that the API version should be read from.
    /// </param>
    public WolverineApiVersioningOptions ReadFromHeader(string headerName = "X-Api-Version")
    {
        VersionReaders.Add(WolverineApiVersionReader.Header(headerName));
        return this;
    }
    
    /// <summary>
    /// Configures API versions to be read from a specified query string parameter.
    /// </summary>
    /// <param name="parameterName">
    /// The name of the query string parameter that the API version should be read from.
    /// </param>
    public WolverineApiVersioningOptions ReadFromQueryString(string parameterName = "api-version")
    {
        VersionReaders.Add(WolverineApiVersionReader.QueryString(parameterName));
        return this;
    }

    /// <summary>
    /// Configures API versions to be read from a specified media type parameter.
    /// </summary>
    /// <inheritdoc cref="MediaTypeVersionReader" path="/remarks"/>
    /// <param name="parameterName">
    /// The name of the media type parameter that the API version should be read from.
    /// </param>
    public WolverineApiVersioningOptions ReadFromMediaType(string parameterName = "v")
    {
        VersionReaders.Add(WolverineApiVersionReader.MediaType(parameterName));
        return this;
    }
}
