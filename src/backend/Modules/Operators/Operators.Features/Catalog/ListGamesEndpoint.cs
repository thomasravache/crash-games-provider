using Apogeu.BuildingBlocks.Security;
using Apogeu.Operators.Features.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Apogeu.Operators.Features.Catalog;

#pragma warning disable MA0025 // Scaffold of SPEC-0020 (no logic yet): implemented in the green phase.

/// <summary><c>GET /api/v1/games</c> (HMAC): the games enabled for the calling operator (SPEC-0020 §6; scaffold).</summary>
internal static class ListGamesEndpoint
{
    public const string Route = "/api/v1/games";

    public static IEndpointRouteBuilder MapListGames(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(Route, Handle)
            .RequireAuthorization(OperatorsAuthentication.OperatorPolicy)
            .WithName("ListGames")
            .WithTags("Operator")
            .WithSummary("The games enabled for the calling operator, with their currencies.")
            .Produces<IReadOnlyList<GameSummaryResponse>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized, ErrorResponse.ContentType);

        return endpoints;
    }

    private static IResult Handle(HttpContext http) => throw new NotImplementedException();
}
#pragma warning restore MA0025
