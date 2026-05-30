using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Wolverine.Http.ApiVersioning;

public abstract record WolverineApiVersionReader
{
    /// <inheritdoc cref="HeaderVersionReader"/>
    public static WolverineApiVersionReader Header(string headerName) =>
        new HeaderVersionReader(headerName);

    /// <inheritdoc cref="QueryStringVersionReader"/>
    public static WolverineApiVersionReader QueryString(string parameterName) =>
        new QueryStringVersionReader(parameterName);

    /// <inheritdoc cref="MediaTypeVersionReader"/>
    public static WolverineApiVersionReader MediaType(string parameterName) =>
        new MediaTypeVersionReader(parameterName);

    public abstract string? ReadVersion(HttpRequest request);
}

/// <summary>
/// Reads the API version from the header with the given name.
/// </summary>
/// <param name="HeaderName">The name of the header that contains the API version.</param>
public sealed record HeaderVersionReader(string HeaderName) : WolverineApiVersionReader
{
    public override string? ReadVersion(HttpRequest request)
    {
        var values = request.Headers[HeaderName];
        return values.Count > 0 ? values[0] : null;
    }
}

/// <summary>
/// Reads the API version from the query string parameter with the given name.
/// </summary>
/// <param name="ParameterName">
/// The name of the query string parameter that contains the API version.
/// </param>
public sealed record QueryStringVersionReader(string ParameterName) : WolverineApiVersionReader
{
    public override string? ReadVersion(HttpRequest request)
    {
        var values = request.Query[ParameterName];
        return values.Count > 0 ? values[0] : null;
    }
}

/// <summary>
/// Reads the API version from the media type parameter with the given name.
/// </summary>
/// <remarks>
/// This reader checks both the <c>Content-Type</c> header and the <c>Accept</c> header, in that order,
/// and returns the first matching parameter value it finds.
/// </remarks>
/// <param name="ParameterName">The name of the media type parameter that contains the API version.</param>
public sealed record MediaTypeVersionReader(string ParameterName) : WolverineApiVersionReader
{
    public override string? ReadVersion(HttpRequest request)
    {
        var headers = request.GetTypedHeaders();

        var fromContentType = readContentType(headers.ContentType, ParameterName);
        if (!string.IsNullOrEmpty(fromContentType))
            return fromContentType;

        return readAccept(headers.Accept, ParameterName);
    }

    private static string? readContentType(MediaTypeHeaderValue? contentType, string parameterName)
    {
        if (contentType is null)
            return null;

        var parameters = contentType.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (
                StringSegment.Equals(
                    parameter.Name,
                    parameterName,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return parameter.Value.Value;
            }
        }

        return null;
    }

    private static string? readAccept(IList<MediaTypeHeaderValue> accept, string parameterName)
    {
        for (var i = 0; i < accept.Count; i++)
        {
            var value = readContentType(accept[i], parameterName);
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }
}
