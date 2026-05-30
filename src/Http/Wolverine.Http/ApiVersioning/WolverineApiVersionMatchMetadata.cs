namespace Wolverine.Http.ApiVersioning;

/// <summary>
/// Lightweight policy-input metadata read by <see cref="ApiVersionEndpointSelectorPolicy"/> for
/// candidate filtering and version comparison. <see cref="Asp.Versioning.ApiVersionMetadata"/> is
/// also attached to the same endpoints by <see cref="ApiVersioningPolicy"/> for header-writer and
/// OpenAPI consumption; the two types coexist with intentionally non-overlapping roles.
/// </summary>
internal sealed record WolverineApiVersionMatchMetadata(string Version);