using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http.AspVersioning.Tests.Extensions;
using static Wolverine.Http.AspVersioning.Tests.Endpoints.Payloads;

namespace Wolverine.Http.AspVersioning.Tests.Endpoints;

public static class WolverineOrdersEndpoints
{
    [WolverineGet("/wolverine/orders"), ApiVersion("1.0")]
    public static OrderV1 GetV10() => OrdersV1_0();

    [WolverineGet("/wolverine/orders"), ApiVersion("1.1")]
    public static OrderV1 GetV11() => OrdersV1_1();

    [WolverineGet("/wolverine/orders"), ApiVersion("2.0")]
    public static OrderV2 GetV20() => OrdersV2_0();

    [WolverineGet("/wolverine/orders/{id}"), ApiVersion("1.0")]
    public static object GetByIdV10(string id) => OrdersV1_0(id);

    [WolverineGet("/wolverine/orders/{id}"), ApiVersion("2.0")]
    public static object GetByIdV20(string id) => OrdersV2_0(id);

    [WolverinePost("/wolverine/orders"), ApiVersion("2.0")]
    public static OrderV2 PostV20() => OrdersV2_0();
}

public static class WolverineProductsEndpoints
{
    [WolverineGet("/wolverine/products"), ApiVersion("1.0", Deprecated = true)]
    public static VersionStamp GetV1() => ProductsV1();

    [WolverineGet("/wolverine/products"), ApiVersion("2.0")]
    public static VersionStamp GetV2() => ProductsV2();
}

public static class WolverineHealthEndpoint
{
    [WolverineGet("/wolverine/health"), ApiVersionNeutral]
    public static VersionStamp Get() => Neutral();
}

public static class WolverineStatusEndpoints
{
    [WolverineGet("/wolverine/status"), ApiVersionNeutral]
    public static VersionStamp GetNeutral() => Neutral();

    [WolverineGet("/wolverine/status"), ApiVersion("2.0")]
    public static VersionStamp GetV2() => StatusV2();
}

public static class WolverineSecureEndpoints
{
    [WolverineGet("/wolverine/secure"), ApiVersion("1.0"), Authorize]
    public static VersionStamp GetV1() => SecureV1();

    [WolverineGet("/wolverine/secure"), ApiVersion("2.0")]
    public static VersionStamp GetV2() => SecureV2();
}

public static class WolverineCombinedEndpoint
{
    [WolverineGet("/wolverine/combined")]
    [ApiVersion("1.0"), ApiVersion("2.0")]
    public static VersionStamp Get(HttpContext ctx) =>
        ctx.GetRequestedApiVersion()?.MajorVersion == 2 ? CombinedV2() : CombinedV1();
}

public static class WolverinePingEndpoint
{
    [WolverineGet("/wolverine/ping")]
    public static VersionStamp Get() => Ping();
}
