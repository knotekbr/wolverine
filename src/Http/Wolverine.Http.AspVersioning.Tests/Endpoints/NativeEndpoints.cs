using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine.Http.AspVersioning.Tests.Extensions;
using static Wolverine.Http.AspVersioning.Tests.Endpoints.Payloads;

namespace Wolverine.Http.AspVersioning.Tests.Endpoints;

public static class NativeEndpoints
{
    public static void MapNativeVersionedEndpoints(this IEndpointRouteBuilder app)
    {
        var v1_0 = new ApiVersion(1, 0);
        var v1_1 = new ApiVersion(1, 1);
        var v2_0 = new ApiVersion(2, 0);

        // -- Orders: 1.0, 1.1, 2.0 ------------------------------------------
        var orders = app.NewVersionedApi("Orders")
            .HasApiVersion(v1_0)
            .HasApiVersion(v1_1)
            .HasApiVersion(v2_0)
            .ReportApiVersions();

        orders.MapGet("/native/orders", () => Results.Ok(OrdersV1_0())).MapToApiVersion(v1_0);
        orders.MapGet("/native/orders", () => Results.Ok(OrdersV1_1())).MapToApiVersion(v1_1);
        orders.MapGet("/native/orders", () => Results.Ok(OrdersV2_0())).MapToApiVersion(v2_0);

        // route param + versioning
        orders
            .MapGet("/native/orders/{id}", (string id) => Results.Ok(OrdersV1_0(id)))
            .MapToApiVersion(v1_0);
        orders
            .MapGet("/native/orders/{id}", (string id) => Results.Ok(OrdersV2_0(id)))
            .MapToApiVersion(v2_0);

        // method differentiation: POST only at v2
        orders.MapPost("/native/orders", () => Results.Ok(OrdersV2_0())).MapToApiVersion(v2_0);

        // -- Products: 1.0 deprecated, 2.0 current --------------------------
        var products = app.NewVersionedApi("Products")
            .HasDeprecatedApiVersion(v1_0)
            .HasApiVersion(v2_0)
            .ReportApiVersions();

        products.MapGet("/native/products", () => Results.Ok(ProductsV1())).MapToApiVersion(v1_0);
        products.MapGet("/native/products", () => Results.Ok(ProductsV2())).MapToApiVersion(v2_0);

        // -- Health: neutral ------------------------------------------------
        var health = app.NewVersionedApi("Health");

        health.MapGet("/native/health", () => Results.Ok(Neutral())).IsApiVersionNeutral();

        // -- Status: neutral + versioned coexistence ------------------------
        var status = app.NewVersionedApi("Status").HasApiVersion(v2_0);

        status.MapGet("/native/status", () => Results.Ok(Neutral())).IsApiVersionNeutral();
        status.MapGet("/native/status", () => Results.Ok(StatusV2())).MapToApiVersion(v2_0);

        // -- Secure: per-version auth ---------------------------------------
        var secure = app.NewVersionedApi("Secure")
            .HasApiVersion(v1_0)
            .HasApiVersion(v2_0)
            .ReportApiVersions();

        secure
            .MapGet("/native/secure", () => Results.Ok(SecureV1()))
            .MapToApiVersion(v1_0)
            .RequireAuthorization(); // v1 requires auth
        secure.MapGet("/native/secure", () => Results.Ok(SecureV2())).MapToApiVersion(v2_0); // v2 anonymous

        // -- Combined: one delegate mapped to two versions (native analog) --
        var combined = app.NewVersionedApi("Combined")
            .HasApiVersion(v1_0)
            .HasApiVersion(v2_0)
            .ReportApiVersions();

        combined
            .MapGet(
                "/native/combined",
                (HttpContext ctx) =>
                {
                    var requested = ctx.GetRequestedApiVersion();
                    return Results.Ok(requested?.MajorVersion == 2 ? CombinedV2() : CombinedV1());
                }
            )
            .HasApiVersion(v1_0)
            .HasApiVersion(v2_0);

        // -- Ping: unversioned ----------------------------------------------
        app.MapGet("/native/ping", () => Results.Ok(Ping()));
    }
}
