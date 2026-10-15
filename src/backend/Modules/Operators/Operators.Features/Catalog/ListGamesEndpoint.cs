using Apogeu.BuildingBlocks.Contracts;
using Apogeu.BuildingBlocks.Security;
using Apogeu.Operators.Features.Authentication;
using Apogeu.Operators.Features.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Apogeu.Operators.Features.Catalog;

/// <summary>
/// <c>GET /api/v1/games</c> (HMAC): the games enabled for the calling operator (SPEC-0020 §6), with the art of their kit
/// (SPEC-0044 §6).
/// </summary>
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
            .WithSummary("The games enabled for the calling operator, with their currencies and the art of their visual kit.")
            .Produces<IReadOnlyList<GameSummaryResponse>>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized, ErrorResponse.ContentType);

        return endpoints;
    }

    private static Ok<IReadOnlyList<GameSummaryResponse>> Handle(HttpContext http, GameCatalog catalog, IOptions<OperatorsOptions> options)
    {
        var gameBaseUrl = options.Value.GameBaseUrl
            ?? throw new InvalidOperationException($"Missing required configuration '{OperatorsOptions.SectionName}:GameBaseUrl'.");
        var operatorId = new OperatorId(http.User.FindFirst(OperatorsAuthentication.OperatorClaim)!.Value);
        IReadOnlyList<GameSummaryResponse> games =
        [
            .. catalog.EnabledFor(operatorId)
                .Select(game => new GameSummaryResponse(
                    game.Code.Value,
                    game.Name,
                    [.. game.Currencies.Select(c => c.Code)],
                    GameKit.ThumbnailUrl(gameBaseUrl, game.Code),
                    GameKit.BannerUrl(gameBaseUrl, game.Code))),
        ];
        return TypedResults.Ok(games);
    }
}
