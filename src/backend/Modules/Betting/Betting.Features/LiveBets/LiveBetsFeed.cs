using System.Collections.Concurrent;
using Apogeu.Betting.Domain;
using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.LiveBets;

/// <summary>
/// Feeds the <see cref="LiveBetsBatcher"/> with the module's own bet events (SPEC-0025 §4): a bet accepted in a round, a
/// queued bet that entered its round, a cancellation and a cash-out. The nickname comes only from the session's claim
/// <c>nick</c> (Emenda v2); a queued bet keeps it here until it enters its round.
/// </summary>
public sealed class LiveBetsFeed(LiveBetsBatcher batcher)
{
    private readonly ConcurrentDictionary<Guid, string?> _queuedNicknames = new();

    /// <summary>The group of the connections that joined the live bets of <paramref name="salaId"/> (<c>JoinSalaBets</c>).</summary>
    public static string GroupOf(string salaId) => "live-bets:" + salaId;

    /// <summary>A bet accepted by <c>PlaceBet</c>: placed in its round, or queued for the next one.</summary>
    public void Accepted(string salaId, Bet bet, string? nickname)
    {
        ArgumentNullException.ThrowIfNull(bet);
        if (bet.Status == BetStatus.Queued || bet.RoundId is null)
        {
            _queuedNicknames[bet.Id] = nickname;
            return;
        }

        Record(salaId, bet, nickname);
    }

    /// <summary>A queued bet that entered the betting phase of its round.</summary>
    public void Activated(string salaId, Bet bet)
    {
        ArgumentNullException.ThrowIfNull(bet);
        _queuedNicknames.TryRemove(bet.Id, out var nickname);
        Record(salaId, bet, nickname);
    }

    /// <summary>A bet cancelled by its player: it leaves the room's round.</summary>
    public void Cancelled(string salaId, Bet bet)
    {
        ArgumentNullException.ThrowIfNull(bet);
        _queuedNicknames.TryRemove(bet.Id, out _);
        if (bet.RoundId is { } roundId)
        {
            batcher.Forget(salaId, roundId.Value, bet.Id);
        }
    }

    /// <summary>A bet cashed out at <paramref name="multiplier"/> with <paramref name="payout"/>.</summary>
    public void CashedOut(Bet bet, decimal multiplier, decimal payout)
    {
        ArgumentNullException.ThrowIfNull(bet);
        if (bet.RoundId is { } roundId && batcher.SalaOf(roundId.Value) is { } salaId)
        {
            batcher.Record(new LiveBetCashedOut(salaId, roundId.Value, bet.Id, new Multiplier(multiplier), new Money(payout)));
        }
    }

    private void Record(string salaId, Bet bet, string? nickname)
    {
        if (bet.RoundId is { } roundId)
        {
            batcher.Record(new LiveBetPlaced(salaId, roundId.Value, bet.Id, bet.Owner, nickname, new Money(bet.Amount)));
        }
    }
}
