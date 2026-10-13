using Apogeu.BuildingBlocks.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Apogeu.Betting.Features.MyBets;

/// <summary>
/// <c>GET /api/v1/me/bets?limit=1..50&amp;before=&lt;cursor&gt;</c> (player session): the personal history of the session's
/// player (SPEC-0025 §6); <c>400 INVALID_LIMIT</c> for a limit outside 1..50, <c>401</c> without a session.
/// </summary>
internal static class GetMyBetsEndpoint
{
    public const string Route = "/api/v1/me/bets";

    /// <summary>Smallest <c>limit</c>.</summary>
    public const int MinLimit = 1;

    /// <summary>Largest <c>limit</c>.</summary>
    public const int MaxLimit = 50;

    public static IEndpointRouteBuilder MapGetMyBets(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(Route, Handle)
            .RequireAuthorization(SecurityPolicies.PlayerSession)
            .WithName("GetMyBets")
            .WithTags("Betting")
            .WithSummary("The bets of the session player, most recent first, paged by an opaque cursor.")
            .Produces<MyBetsPage>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest, ErrorResponse.ContentType)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized, ErrorResponse.ContentType);

        return endpoints;
    }

    // Scaffold (SPEC-0025 phase 1): answers 501 until the history is implemented.
    private static Results<Ok<MyBetsPage>, JsonHttpResult<ErrorResponse>> Handle(HttpContext http, int? limit, string? before) =>
        TypedResults.Json(
            new ErrorResponse("Not implemented.", StatusCodes.Status501NotImplemented, "NOT_IMPLEMENTED"),
            statusCode: StatusCodes.Status501NotImplemented,
            contentType: ErrorResponse.ContentType);
}
