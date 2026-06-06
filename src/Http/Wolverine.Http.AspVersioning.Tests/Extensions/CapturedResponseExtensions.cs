using System.Text.Json;
using Shouldly;
using Wolverine.Http.AspVersioning.Tests.TestSupport;

namespace Wolverine.Http.AspVersioning.Tests.Extensions;

public static class CapturedResponseExtensions
{
    /// <summary>
    /// Headers compared by default. Version-info headers are the high-value ones.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultHeadersToCompare =
    [
        "api-supported-versions",
        "api-deprecated-versions",
        "sunset",
        "deprecation",
        "link",
        "content-type",
    ];

    /// <summary>
    /// Assert that this Wolverine response is "equivalent" to the given native response, meaning:
    /// <list type="bullet">
    /// <item>Status codes match</item>
    /// <item>Bodies match</item>
    /// <item>All compared headers match</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Bodies and headers are normalized before comparison to allow for non-significant differences
    /// (e.g. header value order, whitespace, etc.).
    /// </remarks>
    /// <param name="wolverine">The Wolverine response being compared.</param>
    /// <param name="native">The native response to compare against.</param>
    /// <param name="headersToCompare">
    /// Optional headers to explicitly compare. Defaults to <see cref="DefaultHeadersToCompare"/>
    /// if not specified.
    /// </param>
    /// <param name="headersToIgnore">Optional headers to ignore during comparison.</param>
    public static void ShouldHaveEquivalentNativeResponse(
        this CapturedResponse wolverine,
        CapturedResponse native,
        IEnumerable<string>? headersToCompare = null,
        IEnumerable<string>? headersToIgnore = null
    )
    {
        var comparedHeaders = resolveComparedHeaders(headersToCompare, headersToIgnore);

        // 1. Status code must match exactly.
        wolverine.StatusCode.ShouldBe(
            native.StatusCode,
            $"Status mismatch for {wolverine.Method} {wolverine.Path}: "
                + $"native={native.StatusCode}, wolverine={wolverine.StatusCode}"
        );

        // 2. Normalized bodies must match.
        normalizeBody(wolverine.Body)
            .ShouldBe(
                normalizeBody(native.Body),
                $"Body mismatch for {wolverine.Method} {wolverine.Path}"
            );

        // 3. Each compared (normalized) header must match.
        foreach (var name in comparedHeaders)
        {
            var n = normalizeHeader(native.Headers, name);
            var w = normalizeHeader(wolverine.Headers, name);

            w.ShouldBe(
                n,
                $"Header '{name}' mismatch for {wolverine.Method} {wolverine.Path}: "
                    + $"native=[{n}], wolverine=[{w}]"
            );
        }
    }

    private static IEnumerable<string> resolveComparedHeaders(
        IEnumerable<string>? headersToCompare,
        IEnumerable<string>? headersToIgnore
    )
    {
        var comparedHeaders = headersToCompare ?? DefaultHeadersToCompare;

        if (headersToIgnore?.Any() == true)
            comparedHeaders = comparedHeaders.Except(
                headersToIgnore,
                StringComparer.OrdinalIgnoreCase
            );

        return comparedHeaders;
    }

    /// <summary>
    /// Sorted, trimmed, comma-split normalization so "1.0, 2.0" == "2.0,1.0".
    /// </summary>
    private static string normalizeHeader(IDictionary<string, string[]> headers, string name)
    {
        if (!headers.TryGetValue(name, out var values) || values.Length == 0)
            return "<absent>";

        var parts = values
            .SelectMany(v =>
                v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            )
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        return string.Join(",", parts);
    }

    private static string normalizeBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        try
        {
            // canonical re-serialization
            using var doc = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(doc.RootElement);
        }
        catch
        {
            return body.Trim();
        }
    }
}
