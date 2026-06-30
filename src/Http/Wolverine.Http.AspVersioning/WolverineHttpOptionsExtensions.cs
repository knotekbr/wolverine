namespace Wolverine.Http.AspVersioning;

public static class WolverineHttpOptionsExtensions
{
    public static void UseAspVersioning(this WolverineHttpOptions httpOptions)
    {
        if (!httpOptions.Policies.Any(policy => policy is AspVersioningPolicy))
            httpOptions.AddPolicy<AspVersioningPolicy>();
    }
}
