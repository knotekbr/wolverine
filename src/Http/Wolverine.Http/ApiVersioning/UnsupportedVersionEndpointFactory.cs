using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Wolverine.Http.ApiVersioning;

internal static class UnsupportedVersionEndpointFactory
{
    // The Version field is unused by WriteVersioningHeadersTo - only Sunset/Deprecation are read.
    // Using ApiVersion.Default because the unsupported-version path has no "selected version".
    private static readonly ApiVersionEndpointHeaderState _emptyHeaderState = new(
        ApiVersion.Default,
        null,
        null
    );

    public static Endpoint Create(
        WolverineApiVersioningOptions options,
        ApiVersionHeaderWriter headerWriter,
        string? requestedVersion,
        ApiVersionMetadata? siblingMetadata
    )
    {
        var statusCode = options.UnsupportedApiVersionStatusCode;
        var metadata = new EndpointMetadataCollection(
            siblingMetadata ?? new ApiVersionMetadata(ApiVersionModel.Empty, ApiVersionModel.Empty)
        );

        return new Endpoint(
            async ctx =>
            {
                headerWriter.WriteVersioningHeadersTo(ctx, _emptyHeaderState);

                var problemDetails = new ProblemDetails
                {
                    Detail = requestedVersion is null
                        ? "No API version was specified."
                        : $"The requested API version '{requestedVersion}' is not supported.",
                    Status = statusCode,
                    Title = "Unsupported API version",
                    Type = "https://docs.api-versioning.org/problems#unsupported",
                };

                await HttpHandler.WriteProblems(problemDetails, ctx).ConfigureAwait(false);
            },
            metadata,
            $"Wolverine: Unsupported API Version '{requestedVersion ?? "(unspecified)"}'"
        );
    }
}
