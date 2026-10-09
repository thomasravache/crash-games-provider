using Microsoft.AspNetCore.Routing;

namespace Apogeu.Operators.Features.Sessions;

#pragma warning disable MA0025 // Scaffold of SPEC-0009 (no logic yet): implemented in the green phase.

/// <summary><c>POST /api/v1/sessions</c>: exchanges a launch token for a player session (SPEC-0009 §6).</summary>
internal static class CreateSessionEndpoint
{
    public const string Route = "/api/v1/sessions";

    public static IEndpointRouteBuilder MapCreateSession(this IEndpointRouteBuilder endpoints) => throw new NotImplementedException();
}
#pragma warning restore MA0025
