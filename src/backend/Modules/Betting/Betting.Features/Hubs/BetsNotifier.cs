using Apogeu.Betting.Domain;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>Sends the server messages of <see cref="BetsHub"/> to the connections of one player only (their group).</summary>
public sealed class BetsNotifier(IHubContext<BetsHub, IBetsClient> hub)
{
    /// <summary>The group of every connection of the player of <paramref name="owner"/>.</summary>
    public static string GroupOf(BetOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return $"bets:{owner.OperatorId.Value}:{owner.PlayerId.Value}";
    }

    /// <summary><c>CashedOut</c> of an automatic cash-out, or of a pending one the leader accepted later (SPEC-0023).</summary>
    public Task CashedOutAsync(BetOwner owner, CashedOut message) => hub.Clients.Group(GroupOf(owner)).CashedOut(message);

    /// <summary><c>CashOutRejected</c> of a pending cash-out the leader refused later (SPEC-0023).</summary>
    public Task CashOutRejectedAsync(BetOwner owner, CashOutRejected message) => hub.Clients.Group(GroupOf(owner)).CashOutRejected(message);

    /// <summary><c>BalanceUpdated</c> once a credit retried later is confirmed (SPEC-0023).</summary>
    public Task BalanceUpdatedAsync(BetOwner owner, BalanceUpdatedMessage message) => hub.Clients.Group(GroupOf(owner)).BalanceUpdated(message);

    /// <summary><c>BetActivated</c> of a queued bet that entered the betting phase of its round.</summary>
    public Task BetActivatedAsync(Bet bet)
    {
        ArgumentNullException.ThrowIfNull(bet);
        var round = bet.RoundId ?? throw new InvalidOperationException($"Bet {bet.Id} has no round to be activated in.");
        return hub.Clients.Group(GroupOf(bet.Owner)).BetActivated(new BetActivatedMessage(bet.Id, round.Value, bet.RoundNumber!.Value));
    }
}
