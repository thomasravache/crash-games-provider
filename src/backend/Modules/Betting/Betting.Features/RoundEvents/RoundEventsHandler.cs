using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Infrastructure.Persistence;
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

    public static async Task HandleAsync(RoundFlying flying, IRoundFeed feed, IBetStore store, BettingMetrics metrics, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(flying);
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(metrics);

        feed.Apply(flying);
        metrics.BetReached("active", await store.StartFlightAsync(flying.RoundId, ct).ConfigureAwait(false));
    }

    /// <summary>The settlement of the round's bets is SPEC-0024's; Betting only closes the round.</summary>
    public static void Handle(RoundCrashed crashed, IRoundFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        feed.Apply(crashed);
    }

    /// <summary>The refund of the annulled round's bets is SPEC-0024's; Betting only closes the round.</summary>
    public static void Handle(RoundAnnulled annulled, IRoundFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        feed.Apply(annulled);
    }
}
