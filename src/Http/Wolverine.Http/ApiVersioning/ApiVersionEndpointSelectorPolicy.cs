using System.Diagnostics.CodeAnalysis;
using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

namespace Wolverine.Http.ApiVersioning;

internal sealed class ApiVersionEndpointSelectorPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    // This policy is registered unconditionally. Allowing options to be null enables us to short-circuit
    // the policy when API versioning is not enabled or not configured with a non-URL reader.
    private readonly WolverineApiVersioningOptions? _options;
    private readonly ApiVersionHeaderWriter _headerWriter;

    private readonly string? _defaultVersion;

    public ApiVersionEndpointSelectorPolicy(
        WolverineApiVersioningOptions? options,
        ApiVersionHeaderWriter headerWriter
    )
    {
        _options = options;
        _headerWriter = headerWriter;

        _defaultVersion = options is { AssumeDefaultVersionWhenUnspecified: true }
            ? options.DefaultVersion?.ToString()
            : null;
    }

    // Run after built-in policies and ContentTypeEndpointSelectorPolicy (order 100).
    public override int Order => 200;

    [MemberNotNullWhen(false, nameof(_options))]
    private bool shouldShortCircuit => _options is not { UsesNonUrlReader: true };

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        if (shouldShortCircuit)
            return false;

        for (var i = 0; i < endpoints.Count; i++)
        {
            if (endpoints[i].Metadata.GetMetadata<WolverineApiVersionMatchMetadata>() != null)
            {
                return true;
            }
        }

        return false;
    }

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        if (shouldShortCircuit)
            return Task.CompletedTask;

        var requestedVersion = readRequestedVersion(_options, _defaultVersion, httpContext.Request);
        var anyMatched = false;
        var anyVersioned = false;

        // Save one ApiVersionMetadata instance in case we need to build an unsupported endpoint.
        // Any valid sibling's metadata works here because ApiVersioningPolicy.AttachMetadata seeds
        // every sibling at the same (verb, route-after-strip-prefix) with the full union of implemented
        // versions. Picking the first one gives the same answer as picking any other.
        ApiVersionMetadata? siblingMetadata = null;

        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
                continue;

            var endpoint = candidates[i].Endpoint;
            var metadata = endpoint?.Metadata.GetMetadata<WolverineApiVersionMatchMetadata>();
            if (metadata is null)
                continue;

            anyVersioned = true;
            siblingMetadata ??= endpoint?.Metadata.GetMetadata<ApiVersionMetadata>();

            if (
                requestedVersion is not null
                && string.Equals(
                    metadata.Version,
                    requestedVersion,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                anyMatched = true;
            }
            else
            {
                candidates.SetValidity(i, false);
            }
        }

        // Happy path: either a matching version was found or no versioned endpoints were present.
        if (anyMatched || !anyVersioned)
            return Task.CompletedTask;

        // Sad path: versioned endpoints exist but none matched the requested version - fail the request.
        var unsupportedEndpoint = UnsupportedVersionEndpointFactory.Create(
            _options,
            _headerWriter,
            requestedVersion,
            siblingMetadata
        );
        httpContext.SetEndpoint(unsupportedEndpoint);

        return Task.CompletedTask;
    }

    private static string? readRequestedVersion(
        WolverineApiVersioningOptions options,
        string? defaultVersion,
        HttpRequest request
    )
    {
        foreach (var strategy in options.VersionReaders)
        {
            var version = strategy.ReadVersion(request);
            if (!string.IsNullOrEmpty(version))
                return version;
        }

        return defaultVersion;
    }
}
