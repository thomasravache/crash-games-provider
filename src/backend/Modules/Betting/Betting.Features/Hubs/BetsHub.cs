using Apogeu.Betting.Features.CancelBet;
using Apogeu.Betting.Features.CashOut;
using Apogeu.Betting.Features.LiveBets;
using Apogeu.Betting.Features.PlaceBet;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Features.Sessions;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.BuildingBlocks.Security;
using Apogeu.Operators.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>
/// Hub <c>/hubs/bets</c> (SPEC-0022 §6, ADR-0005): only translates messages to the commands of the module; requires a player
/// session (SPEC-0009), whose claims give the player, operator, currency, room and mode. Errors are <see cref="HubException"/>
/// whose message is the code (<c>BettingErrorCodes</c>).
/// </summary>
[Authorize(Policy = SecurityPolicies.PlayerSession)]
public sealed class BetsHub(
    PlaceBetUseCase placeBet,
    CancelBetUseCase cancelBet,
    CashOutUseCase cashOut,
    IOperatorLimits limits,
    LiveBetsBatcher liveBets,
    IRoundFeed feed,
    TimeProvider time)
    : Hub<IBetsClient>
{
    public const string Path = "/hubs/bets";

    /// <summary>Every connection of the player joins the player's group, the only target of <c>BetActivated</c>.</summary>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, BetsNotifier.GroupOf(Session.Owner)).ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>Debits and accepts a bet (placed or queued); repeating the <c>clientBetId</c> returns the original result.</summary>
    public async Task<BetAccepted> PlaceBet(PlaceBetRequest request) =>
        Answer(await placeBet.PlaceAsync(Session, request, Context.ConnectionAborted).ConfigureAwait(false));

    /// <summary>Cancels a placed or queued bet of the session's player with its full amount refunded.</summary>
    public async Task<BetCancelled> CancelBet(CancelBetRequest request) =>
        Answer(await cancelBet.CancelAsync(Session, request, Context.ConnectionAborted).ConfigureAwait(false));

    /// <summary>
    /// Cashes out an active bet of the session's player (SPEC-0023 §6): the leader of the room decides with the server instant
    /// of reception. Errors: <c>CASHOUT_TOO_LATE</c>, <c>BET_NOT_ACTIVE</c>, <c>BET_NOT_FOUND</c>.
    /// </summary>
    public async Task<CashOutResult> CashOut(CashOutRequest request)
    {
        // The instant of the cash-out is the server clock when the request is received (ADR-0005, tolerance 0).
        var receivedAt = time.GetUtcNow();
        return Answer(await cashOut.CashOutAsync(Session, request, receivedAt).ConfigureAwait(false));
    }

    /// <summary>The bet limits of the session's operator in its currency (SPEC-0020@1).</summary>
    public Task<BetLimitsResponse> GetBetLimits()
    {
        var owner = Session.Owner;
        var betLimits = limits.Get(owner.OperatorId, owner.Currency);
        return Task.FromResult(new BetLimitsResponse(betLimits.Currency.Code, betLimits.MinBet, betLimits.MaxBet, betLimits.MaxWin));
    }

    /// <summary>
    /// Joins the group of the session's room for <c>LiveBetsBatch</c> and answers the bets of its current round (SPEC-0025
    /// §6); takes no argument: the room is the session's.
    /// </summary>
    public async Task<LiveBetsSnapshot> JoinSalaBets()
    {
        var salaId = Session.SalaId;

        // Joined before the snapshot is read, so no batch falls between the two (a repeated bet in both is the same betRef).
        await Groups.AddToGroupAsync(Context.ConnectionId, LiveBetsFeed.GroupOf(salaId)).ConfigureAwait(false);
        var current = await feed.CurrentAsync(salaId, Context.ConnectionAborted).ConfigureAwait(false);
        if (current is not null && liveBets.SnapshotOf(salaId) is { } snapshot && current.RoundId.Value == snapshot.RoundId)
        {
            return snapshot;
        }

        // A round without bets yet: empty, of the current round; no current round: empty, never the previous round's bets.
        return new LiveBetsSnapshot(current?.RoundId.Value ?? Guid.Empty, 0, 0, new Money(0.00m), []);
    }

    private BettingSession Session => BettingSession.From(Context.User);

    private static T Answer<T>(BettingOutcome<T> outcome)
        where T : class =>
        outcome.Value ?? throw new HubException(outcome.Code);
}
