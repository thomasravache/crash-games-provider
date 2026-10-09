using Microsoft.AspNetCore.Routing;

namespace Apogeu.Operators.Features.Backoffice;

#pragma warning disable MA0025 // Scaffold of SPEC-0009 (no logic yet): implemented in the green phase.

/// <summary><c>GET /api/v1/backoffice/me</c> (Keycloak bearer): proves the backoffice identity (SPEC-0009 §6).</summary>
internal static class GetMeEndpoint
{
    public const string Route = "/api/v1/backoffice/me";

    public static IEndpointRouteBuilder MapGetMe(this IEndpointRouteBuilder endpoints) => throw new NotImplementedException();
}
#pragma warning restore MA0025
