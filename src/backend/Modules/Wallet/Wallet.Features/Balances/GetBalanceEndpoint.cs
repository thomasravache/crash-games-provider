using Apogeu.BuildingBlocks.Security;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Features.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Apogeu.Wallet.Features.Balances;

/// <summary><c>GET /api/v1/wallet/balance</c> (player session): the balance of the session's own player (SPEC-0021 §6).</summary>
internal static class GetBalanceEndpoint
{
    public const string Route = "/api/v1/wallet/balance";

    public static IEndpointRouteBuilder MapGetBalance(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(Route, HandleAsync)
            .RequireAuthorization(SecurityPolicies.PlayerSession)
            .WithName("GetWalletBalance")
            .WithTags("Wallet")
            .WithSummary("The balance of the session player in the session currency.")
            .Produces<BalanceResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized, ErrorResponse.ContentType);

        return endpoints;
    }

    private static async Task<Ok<BalanceResponse>> HandleAsync(HttpContext http, IOperatorWallet wallet, CancellationToken ct)
    {
        var session = WalletSession.Of(http.User);
        var balance = await wallet.GetBalanceAsync(session.Player, session.Currency, ct).ConfigureAwait(false);
        return TypedResults.Ok(new BalanceResponse(balance.Amount, balance.Currency.Code, WalletSession.DemoMode));
    }
}
