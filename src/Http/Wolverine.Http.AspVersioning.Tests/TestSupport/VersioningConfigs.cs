using Asp.Versioning;

namespace Wolverine.Http.AspVersioning.Tests.TestSupport;

public static class VersioningConfigs
{
    public static Action<ApiVersioningOptions> QueryReader(string name = "api-version") =>
        o =>
        {
            o.ReportApiVersions = true;
            o.ApiVersionReader = new QueryStringApiVersionReader(name);
        };

    public static Action<ApiVersioningOptions> HeaderReader(string name = "X-Api-Version") =>
        o =>
        {
            o.ReportApiVersions = true;
            o.ApiVersionReader = new HeaderApiVersionReader(name);
        };

    public static Action<ApiVersioningOptions> MediaTypeReader(string param = "v") =>
        o =>
        {
            o.ReportApiVersions = true;
            o.ApiVersionReader = new MediaTypeApiVersionReader(param);
        };

    public static Action<ApiVersioningOptions> UrlSegmentReader() =>
        o =>
        {
            o.ReportApiVersions = true;
            o.ApiVersionReader = new UrlSegmentApiVersionReader();
        };

    public static Action<ApiVersioningOptions> Combined() =>
        o =>
        {
            o.ReportApiVersions = true;
            o.ApiVersionReader = ApiVersionReader.Combine(
                new HeaderApiVersionReader("X-Api-Version"),
                new QueryStringApiVersionReader("api-version")
            );
        };

    public static Action<ApiVersioningOptions> AssumeDefault(ApiVersion @default) =>
        o =>
        {
            o.ReportApiVersions = true;
            o.AssumeDefaultVersionWhenUnspecified = true;
            o.DefaultApiVersion = @default;
            o.ApiVersionReader = new HeaderApiVersionReader("X-Api-Version");
        };

    public static Action<ApiVersioningOptions> WithSelector(IApiVersionSelector selector) =>
        o =>
        {
            o.ReportApiVersions = true;
            o.AssumeDefaultVersionWhenUnspecified = true;
            o.ApiVersionSelector = selector;
            o.ApiVersionReader = new HeaderApiVersionReader("X-Api-Version");
        };
}
