using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.Rounds.Contracts;
using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features.Settlement;

/// <summary>
/// Closes the bets of a round when it ends (SPEC-0024 §6), idempotent by round: on the crash its active bets without a pending
/// cash-out request become lost in one statement (<c>BetSettled</c> after the commit); on the annulment its placed and active
/// bets become refunded, each with its durable refund (<c>BetRefunded</c> comes from the refund). Whoever closes the last open
/// bet of an ended round closes its summary, once.
/// </summary>
public sealed class RoundSettler(IBetStore store, BetsNotifier notifier, BettingMetrics metrics, TimeProvider time, ILogger<RoundSettler> logger)
{
    /// <summary>The <c>BetSettled</c> messages sent at a time after a crash.</summary>
    private const int NotifyBatch = 200;

    public const string Crashed = "crashed";

    public const string Annulled = "annulled";

    /// <summary>
    /// How long after the end of a crashed round its summary stays open (SPEC-0024 review G4, M1): the decision deadline of a
    /// cash-out received before the crash plus the relay to the leader, so an accepted one can still turn a lost bet into
    /// cashed out.
    /// </summary>
    public static readonly TimeSpan SummaryGrace = CashOut.CashOutUseCase.DecisionDeadline + TimeSpan.FromMilliseconds(300);

    private static readonly TimeSpan GraceMargin = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The round crashed (after the reconciliation of its automatic cash-outs, SPEC-0023): its active bets without a pending
    /// request become lost, its summary closes when nothing is left open, and the players are told.
    /// </summary>
    public async Task SettleCrashAsync(RoundId roundId, CancellationToken ct)
    {
        var started = time.GetTimestamp();
        var endedAt = time.GetUtcNow();
        await store.MarkRoundEndedAsync(roundId, Crashed, endedAt, ct).ConfigureAwait(false);
        var lost = await SettleLostAsync(roundId, ct).ConfigureAwait(false);

        // A bet still placed (its activation was lost) never flew: refunded in full, with a reconciliation alert (IT-07).
        var placed = await store.RefundRoundAsync(roundId, placedOnly: true, ct).ConfigureAwait(false);
        foreach (var bet in placed)
        {
            BettingLog.PlacedAtCrash(logger, bet.Id, roundId.Value);
        }

        metrics.BetsSettled("refunded", placed.Count);
        metrics.RefundPending(placed.Count);
        metrics.SettledIn(time.GetElapsedTime(started));

        // The summary closes once a cash-out received before the crash can no longer revive a lost bet.
        // (A margin over the grace: the delay's timer and the wall clock the grace is checked against may differ slightly.)
        var wait = endedAt + SummaryGrace + GraceMargin - time.GetUtcNow();
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, time, ct).ConfigureAwait(false);
        }

        await CloseAsync(roundId, ct).ConfigureAwait(false);
        await NotifyLostAsync(roundId, lost).ConfigureAwait(false);
    }

    /// <summary>The round was annulled: its placed and active bets (pending requests included) are refunded in full.</summary>
    public async Task RefundAnnulledAsync(RoundId roundId, CancellationToken ct)
    {
        var started = time.GetTimestamp();
        await store.MarkRoundEndedAsync(roundId, Annulled, time.GetUtcNow(), ct).ConfigureAwait(false);
        var refunded = await store.RefundRoundAsync(roundId, placedOnly: false, ct).ConfigureAwait(false);
        metrics.BetsSettled("refunded", refunded.Count);
        metrics.RefundPending(refunded.Count);
        BettingLog.RoundSettled(logger, roundId.Value, refunded.Count, "refunded");

        // No cash-out of an annulled round is accepted (TooLate on both paths): its summary needs no grace.
        await CloseAsync(roundId, TimeSpan.Zero, ct).ConfigureAwait(false);
        metrics.SettledIn(time.GetElapsedTime(started));
    }

    /// <summary>
    /// A pending cash-out of <paramref name="roundId"/> was refused (received at or after the crash): once Betting processed the
    /// crash, the bet — now without a request — is lost like the others (before that, the crash itself settles it).
    /// </summary>
    public async Task SettleRefusedAsync(RoundId roundId, CancellationToken ct)
    {
        if (string.Equals(await store.RoundOutcomeAsync(roundId, ct).ConfigureAwait(false), Crashed, StringComparison.Ordinal))
        {
            var lost = await SettleLostAsync(roundId, ct).ConfigureAwait(false);
            await CloseAsync(roundId, ct).ConfigureAwait(false);
            await NotifyLostAsync(roundId, lost).ConfigureAwait(false);
        }
    }

    /// <summary>Closes the summary of the round when it ended (past its grace) and no bet of it is open (idempotent).</summary>
    public Task CloseAsync(RoundId roundId, CancellationToken ct) => CloseAsync(roundId, SummaryGrace, ct);

    private async Task CloseAsync(RoundId roundId, TimeSpan grace, CancellationToken ct)
    {
        if (await store.CloseSettlementAsync(roundId, time.GetUtcNow(), grace, ct).ConfigureAwait(false))
        {
            BettingLog.SummaryClosed(logger, roundId.Value);
        }
    }

    private async Task<IReadOnlyList<Bet>> SettleLostAsync(RoundId roundId, CancellationToken ct)
    {
        var lost = await store.SettleLostAsync(roundId, ct).ConfigureAwait(false);
        metrics.BetsSettled("lost", lost.Count);
        BettingLog.RoundSettled(logger, roundId.Value, lost.Count, "lost");
        return lost;
    }

    /// <summary>After the commit: <c>BetSettled</c> to each player, a batch at a time; a message that fails does not stop the others.</summary>
    private async Task NotifyLostAsync(RoundId roundId, IReadOnlyList<Bet> lost)
    {
        foreach (var batch in lost.Chunk(NotifyBatch))
        {
            await Task.WhenAll(batch.Select(bet => SendAsync(bet, new BetSettled(bet.Id, roundId.Value, BetSettledStatus.Lost)))).ConfigureAwait(false);
        }
    }

    private async Task SendAsync(Bet bet, BetSettled message)
    {
        try
        {
            await notifier.BetSettledAsync(bet.Owner, message).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The bet is settled; only the message to the player failed.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BettingLog.SettlementNotSent(logger, bet.Id, exception);
        }
    }
}
