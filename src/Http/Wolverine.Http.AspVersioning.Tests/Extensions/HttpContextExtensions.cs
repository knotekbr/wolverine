using Asp.Versioning;
using Microsoft.AspNetCore.Http;

namespace Wolverine.Http.AspVersioning.Tests.Extensions;

public static class HttpContextExtensions
{
    public static ApiVersion? GetRequestedApiVersion(this HttpContext context) =>
        context.Features.Get<IApiVersioningFeature>()?.RequestedApiVersion;
}
