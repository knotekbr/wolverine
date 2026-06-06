namespace Wolverine.Http.AspVersioning.Tests.TestSupport;

/// <summary>
/// Captured response surface we care about for comparison.
/// </summary>
public sealed record CapturedResponse(
    string Path,
    string Method,
    int StatusCode,
    string Body,
    IDictionary<string, string[]> Headers
);
