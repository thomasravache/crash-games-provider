using Microsoft.AspNetCore.Routing;

namespace Apogeu.Operators.Features.Launch;

#pragma warning disable MA0025 // Scaffold of SPEC-0009 (no logic yet): implemented in the green phase.

/// <summary><c>POST /api/v1/operator/launch</c> (HMAC): issues a launch URL for a player (SPEC-0009 §6).</summary>
internal static class LaunchEndpoint
{
    public const string Route = "/api/v1/operator/launch";

    public static IEndpointRouteBuilder MapLaunch(this IEndpointRouteBuilder endpoints) => throw new NotImplementedException();
}
#pragma warning restore MA0025
