using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.CashOut;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features.RoundEvents;

/// <summary>
/// Wolverine handler of the Rounds integration events (SPEC-0022 §6), delivered in order by one sequential durable local
/// queue (ADR-0002): each event is applied to the <see cref="IRoundFeed"/> first, then to the bets. Opening a round moves
/// the room's queued bets into it (<c>BetActivated</c>); its flight makes its placed bets active.
/// </summary>
public static class RoundEventsHandler
{
    public static async Task HandleAsync(
        RoundBettingOpened opened,
        IRoundFeed feed,
        IBetStore store,
        BetsNotifier notifier,
        BettingMetrics metrics,
        ILogger<RoundBettingOpened> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(metrics);

        // Applied before the activation (emenda 3), so a bet racing the opening already sees the round open.
        feed.Apply(opened);
        var window = new RoundWindow(opened.RoundId, opened.RoundNumber, opened.BettingEndsAt, RoundPhase.Betting);
        var activated = await store.ActivateQueuedAsync(opened.SalaId, window, betId: null, ct).ConfigureAwait(false);
        if (activated.Count == 0)
        {
            return;
        }

        BettingLog.QueuedActivated(logger, activated.Count, opened.SalaId, opened.RoundId.Value);
        metrics.BetReached("placed", activated.Count);
        foreach (var bet in activated)
        {
            try
            {
                await notifier.BetActivatedAsync(bet).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // One notification that fails (the backplane) does not stop the others; the bet is placed anyway.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                BettingLog.ActivationNotSent(logger, bet.Id, exception);
            }
        }
    }

    /// <summary>
    /// The flight started: the round's placed bets become active, and those with an automatic cash-out are scheduled with the
    /// leader, which executes them at their target (SPEC-0023).
    /// </summary>
    public static async Task HandleAsync(
        RoundFlying flying,
        IRoundFeed feed,
        IBetStore store,
        ICashOutArbiter arbiter,
        BettingMetrics metrics,
        ILogger<RoundFlying> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(flying);
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(arbiter);
        ArgumentNullException.ThrowIfNull(metrics);

        feed.Apply(flying);
        metrics.BetReached("active", await store.StartFlightAsync(flying.RoundId, ct).ConfigureAwait(false));
        foreach (var bet in await store.ListAutoCashOutsAsync(flying.RoundId, ct).ConfigureAwait(false))
        {
            await ScheduleAutoAsync(arbiter, flying.SalaId, bet, logger, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Schedules the automatic cash-out of an active bet with the leader; one that fails does not stop the others.</summary>
    internal static async Task ScheduleAutoAsync(ICashOutArbiter arbiter, string salaId, Bet bet, ILogger logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(arbiter);
        ArgumentNullException.ThrowIfNull(bet);
        if (bet is not { AutoCashOut: { } target, RoundId: { } roundId })
        {
            return;
        }

        try
        {
            await arbiter.ScheduleAutoAsync(salaId, roundId, bet.Id, new Multiplier(target), ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The flight goes on for the other bets; this one keeps its manual cash-out.
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            BettingLog.AutoCashOutNotScheduled(logger, bet.Id, exception);
        }
    }

    /// <summary>
    /// The round crashed, its crash point now public: every active bet of the round not cashed out whose automatic target is
    /// below the crash point is cashed out at the exact target (auto:true) — the leader's schedule in memory is only the fast
    /// path (review G4, M1). The settlement of the other bets is SPEC-0024's.
    /// </summary>
    public static async Task HandleAsync(RoundCrashed crashed, IRoundFeed feed, IBetStore store, CashOutSettlement settlement, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(crashed);
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(settlement);

        feed.Apply(crashed);
        foreach (var bet in await store.ListAutoCashOutsAsync(crashed.RoundId, ct).ConfigureAwait(false))
        {
            if (bet.AutoCashOut is { } target && target < crashed.CrashPoint)
            {
                await settlement.StoreAsync(bet, target, automatic: true, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The refund of the annulled round's bets is SPEC-0024's; Betting only closes the round.</summary>
    public static void Handle(RoundAnnulled annulled, IRoundFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        feed.Apply(annulled);
    }
}
