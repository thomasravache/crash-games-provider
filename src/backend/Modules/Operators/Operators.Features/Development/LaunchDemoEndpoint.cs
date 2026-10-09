using Microsoft.AspNetCore.Routing;

namespace Apogeu.Operators.Features.Development;

#pragma warning disable MA0025 // Scaffold of SPEC-0009 (no logic yet): implemented in the green phase.

/// <summary><c>GET /dev/launch-demo</c>, Development only: redirects (302) to a demo launch URL (SPEC-0009 §6).</summary>
internal static class LaunchDemoEndpoint
{
    public const string Route = "/dev/launch-demo";

    public static IEndpointRouteBuilder MapLaunchDemo(this IEndpointRouteBuilder endpoints) => throw new NotImplementedException();
}
#pragma warning restore MA0025
