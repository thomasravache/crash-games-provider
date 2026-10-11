using Apogeu.BuildingBlocks.Security;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Features.Errors;
using Apogeu.Wallet.Features.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Apogeu.Wallet.Features.Demo;

/// <summary>
/// <c>POST /api/v1/wallet/demo/recharge</c> (player session, demo only): brings the session player's balance back to
/// 3.000,00 when it is below the minimum bet (SPEC-0021 §6); <c>409 RECHARGE_NOT_ALLOWED</c> otherwise, <c>403 NOT_DEMO</c>
/// for a session that is not demo.
/// </summary>
internal static class RechargeEndpoint
{
    public const string Route = "/api/v1/wallet/demo/recharge";

    public static IEndpointRouteBuilder MapRecharge(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(Route, HandleAsync)
            .RequireAuthorization(SecurityPolicies.PlayerSession)
            .WithName("RechargeDemoWallet")
            .WithTags("Wallet")
            .WithSummary("Recharges the demo balance of the session player back to the opening amount when it is below the minimum bet.")
            .Produces<RechargeResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized, ErrorResponse.ContentType)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden, ErrorResponse.ContentType)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict, ErrorResponse.ContentType);

        return endpoints;
    }

    private static async Task<Results<Ok<RechargeResponse>, JsonHttpResult<ErrorResponse>>> HandleAsync(
        HttpContext http, IDemoWallet wallet, CancellationToken ct)
    {
        var session = WalletSession.Of(http.User);
        if (!session.IsDemo)
        {
            return WalletProblems.NotDemo();
        }

        try
        {
            var balance = await wallet.RechargeAsync(session.Player, session.Currency, ct).ConfigureAwait(false);
            return TypedResults.Ok(new RechargeResponse(balance.Amount, balance.Currency.Code));
        }
        catch (RechargeNotAllowedException)
        {
            return WalletProblems.RechargeNotAllowed();
        }
    }
}
