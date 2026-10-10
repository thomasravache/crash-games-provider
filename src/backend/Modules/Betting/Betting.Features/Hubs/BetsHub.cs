using Apogeu.BuildingBlocks.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Betting.Features.Hubs;

#pragma warning disable MA0025 // Scaffold of SPEC-0022 (no logic yet): implemented in the green phase.
#pragma warning disable CA1822 // Scaffold: the hub gets its dependencies in the green phase.

/// <summary>
/// Hub <c>/hubs/bets</c> (SPEC-0022 §6, ADR-0005): only translates messages to the commands of the module; requires a player
/// session (SPEC-0009), whose claims give the player, operator, currency, room and mode. Errors are <see cref="HubException"/>
/// whose message is the code (<c>BettingErrorCodes</c>).
/// </summary>
[Authorize(Policy = SecurityPolicies.PlayerSession)]
public sealed class BetsHub : Hub<IBetsClient>
{
    public const string Path = "/hubs/bets";

    /// <summary>Debits and accepts a bet (placed or queued); repeating the <c>clientBetId</c> returns the original result.</summary>
    public Task<BetAccepted> PlaceBet(PlaceBetRequest request) => throw new NotImplementedException();

    /// <summary>Cancels a placed or queued bet of the session's player with its full amount refunded.</summary>
    public Task<BetCancelled> CancelBet(CancelBetRequest request) => throw new NotImplementedException();

    /// <summary>The bet limits of the session's operator in its currency (SPEC-0020@1).</summary>
    public Task<BetLimitsResponse> GetBetLimits() => throw new NotImplementedException();
}
#pragma warning restore CA1822
#pragma warning restore MA0025
