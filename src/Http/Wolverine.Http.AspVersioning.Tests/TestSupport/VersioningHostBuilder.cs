using Alba;
using Asp.Versioning;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Wolverine.Http.AspVersioning.Tests.TestSupport;

internal class VersioningHostBuilder
{
    private Action<ApiVersioningOptions>? _configureAspVersioning;
    private Action<WolverineHttpOptions>? _configureWolverineHttp;
    private Action<IEndpointRouteBuilder>? _configureNativeEndpoints;

    public VersioningHostBuilder ConfigureAspVersioning(Action<ApiVersioningOptions> configure)
    {
        _configureAspVersioning = configure;
        return this;
    }

    public VersioningHostBuilder ConfigureWolverineHttp(Action<WolverineHttpOptions> configure)
    {
        _configureWolverineHttp = configure;
        return this;
    }

    public VersioningHostBuilder ConfigureNativeEndpoints(Action<IEndpointRouteBuilder> configure)
    {
        _configureNativeEndpoints = configure;
        return this;
    }

    public Task<IAlbaHost> Build(TypeLoadMode codegen = TypeLoadMode.Dynamic)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseWolverine(opts =>
        {
            opts.Discovery.DisableConventionalDiscovery();
            opts.Discovery.IncludeAssembly(GetType().Assembly);
            opts.Services.CritterStackDefaults(x =>
            {
                x.Development.GeneratedCodeMode = codegen;
                x.Production.GeneratedCodeMode = codegen;
                x.Development.ResourceAutoCreate = AutoCreate.None;
                x.Production.ResourceAutoCreate = AutoCreate.None;
            });
        });

        builder.Services.RunWolverineInSoloMode();
        builder.Services.DisableAllExternalWolverineTransports();

        builder.Services.AddWolverineHttp();

        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();

        builder
            .Services.AddApiVersioning(opts =>
            {
                opts.UnsupportedApiVersionStatusCode = 987;

                _configureAspVersioning?.Invoke(opts);
            })
            .AddApiExplorer(o =>
            {
                o.GroupNameFormat = "'v'VVV";
                o.SubstituteApiVersionInUrl = true;
            });

        return AlbaHost.For(
            builder,
            app =>
            {
                app.UseAuthentication();
                app.UseAuthorization();

                _configureNativeEndpoints?.Invoke(app);

                app.MapWolverineEndpoints(opts =>
                {
                    opts.UseApiVersioning(v =>
                    {
                        v.UrlSegmentPrefix = null;
                    });

                    _configureWolverineHttp?.Invoke(opts);
                });
            }
        );
    }
}
