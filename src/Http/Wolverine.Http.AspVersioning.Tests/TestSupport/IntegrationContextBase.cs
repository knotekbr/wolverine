using Alba;
using JasperFx;

namespace Wolverine.Http.AspVersioning.Tests.TestSupport;

public abstract class AppFixtureBase : IAsyncLifetime
{
    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        JasperFxOptions.RememberedApplicationAssembly = GetType().Assembly;

        Host = await BuildHostAsync();
    }

    public async Task DisposeAsync()
    {
        if (Host is null)
            return;

        await Host.StopAsync();

        await Host.DisposeAsync();
        Host = null!;
    }

    protected abstract Task<IAlbaHost> BuildHostAsync();
}

public abstract class IntegrationContextBase : IAsyncLifetime
{
    private readonly AppFixtureBase _fixture;

    public IntegrationContextBase(AppFixtureBase fixture)
    {
        _fixture = fixture;
    }

    public IAlbaHost Host => _fixture.Host;

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync() => Task.CompletedTask;

    public async Task<IScenarioResult> Scenario(Action<Scenario> configure) =>
        await Host.Scenario(configure);

    /// <summary>
    /// Runs the same scenario against both a native endpoint and a Wolverine endpoint.
    /// </summary>
    /// <returns>A tuple containing the responses from both endpoints.</returns>
    public async Task<(
        CapturedResponse NativeResponse,
        CapturedResponse WolverineResponse
    )> DualScenario(HttpRequestMessage request)
    {
        if (request.RequestUri is not { } originalUri)
            throw new InvalidOperationException("Request must have a RequestUri.");

        var nativeResponse = await captureResponse(Host, "/native", request, originalUri);
        var wolverineResponse = await captureResponse(Host, "/wolverine", request, originalUri);

        return (nativeResponse, wolverineResponse);
    }

    private static async Task<CapturedResponse> captureResponse(
        IAlbaHost host,
        string prefix,
        HttpRequestMessage request,
        Uri originalUri
    )
    {
        var result = await host.Scenario(s =>
        {
            s.FromHttpRequestMessage(request).ToUrl(prefix + originalUri.PathAndQuery);
            s.IgnoreStatusCode();
        });

        var body = await result.ReadAsTextAsync();

        var ctx = result.Context;
        var headers = ctx.Response.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.Where(s => s is not null).Cast<string>().ToArray(),
            StringComparer.OrdinalIgnoreCase
        );

        return new CapturedResponse(
            originalUri.AbsolutePath,
            request.Method.Method,
            ctx.Response.StatusCode,
            body,
            headers
        );
    }
}
