using Apogeu.Betting.Features.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features.LiveBets;

/// <summary>
/// Sends the due batches of the <see cref="LiveBetsBatcher"/> to the groups of their rooms as <c>LiveBetsBatch</c>
/// (SPEC-0025 §6, ADR-0019), as the <c>RoundSync</c> of SPEC-0008: the bandwidth of a room does not grow with its players —
/// at most one batch every <see cref="LiveBetsBatcher.Window"/>, each with at most <see cref="LiveBetsBatcher.MaxItems"/> items.
/// </summary>
internal sealed class LiveBetsBroadcaster(
    LiveBetsBatcher batcher,
    IHubContext<BetsHub, IBetsClient> hub,
    BettingMetrics metrics,
    TimeProvider time,
    ILogger<LiveBetsBroadcaster> logger) : BackgroundService
{
    /// <summary>How often the due batches are taken: half a window, so a batch leaves at most 100 ms after it is due.</summary>
    internal static readonly TimeSpan Tick = LiveBetsBatcher.Window / 2;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(Tick, time);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await SendDueAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host stops.
        }
    }

    private async Task SendDueAsync()
    {
        foreach (var (salaId, batch) in batcher.TakeDue())
        {
            try
            {
                await hub.Clients.Group(LiveBetsFeed.GroupOf(salaId)).LiveBetsBatch(batch).ConfigureAwait(false);
                metrics.LiveBatchSent(batch.Placed.Count + batch.CashedOut.Count);
            }
#pragma warning disable CA1031 // One batch that fails (the backplane) does not stop the broadcast; the next one has the totals.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                BettingLog.LiveBatchNotSent(logger, salaId, exception);
            }
        }
    }
}
