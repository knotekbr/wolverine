namespace Wolverine.Http.AspVersioning.Tests.Endpoints;

public record VersionStamp(string Version);

public record OrderV1(string? Id, string Version, string[] Fields);

public record OrderV2(string? Id, string Version, string[] Fields, bool V2Only);

public static class Payloads
{
    public static OrderV1 OrdersV1_0(string? id = null) => new(id, "1.0", ["id", "total"]);

    public static OrderV1 OrdersV1_1(string? id = null) => new(id, "1.1", ["id", "total", "currency"]);

    public static OrderV2 OrdersV2_0(string? id = null) => new(id, "2.0", ["id", "total", "currency"], true);

    public static VersionStamp ProductsV1() => new("1.0");

    public static VersionStamp ProductsV2() => new("2.0");

    public static VersionStamp Neutral() => new("neutral");

    public static VersionStamp StatusV2() => new("2.0");

    public static VersionStamp SecureV1() => new("1.0");

    public static VersionStamp SecureV2() => new("2.0");

    public static VersionStamp CombinedV1() => new("1.0");

    public static VersionStamp CombinedV2() => new("2.0");

    public static VersionStamp Ping() => new("ping");
}
