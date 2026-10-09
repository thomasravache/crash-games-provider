using Apogeu.BuildingBlocks.Contracts;
using Apogeu.BuildingBlocks.Security;
using Apogeu.Operators.Features.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Apogeu.Operators.Features.Catalog;

/// <summary><c>GET /api/v1/games</c> (HMAC): the games enabled for the calling operator (SPEC-0020 §6).</summary>
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

    private static Ok<IReadOnlyList<GameSummaryResponse>> Handle(HttpContext http, GameCatalog catalog)
    {
        var operatorId = new OperatorId(http.User.FindFirst(OperatorsAuthentication.OperatorClaim)!.Value);
        IReadOnlyList<GameSummaryResponse> games =
        [
            .. catalog.EnabledFor(operatorId)
                .Select(game => new GameSummaryResponse(game.Code.Value, game.Name, [.. game.Currencies.Select(c => c.Code)])),
        ];
        return TypedResults.Ok(games);
    }
}
