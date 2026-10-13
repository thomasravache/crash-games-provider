using Apogeu.Wallet.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>
/// Sweep of <c>wallet.pending_resends</c> (SPEC-0046 IT-03, IT-04): takes the credits and rollbacks whose next attempt is due
/// and resends each with the same transaction id, so a pending one is confirmed even after the process restarts.
/// </summary>
internal sealed class SeamlessResendSweep(
    SeamlessResender resender,
    PendingResendStore store,
    TimeProvider clock,
    ILogger<SeamlessResendSweep> logger) : BackgroundService
{
    /// <summary>How often the due rows are looked for.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    /// <summary>Wait after a failed sweep (for example, the schema not migrated yet).</summary>
    internal static readonly TimeSpan FailureInterval = TimeSpan.FromSeconds(5);

    private const int Batch = 32;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = Interval;
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                SeamlessLog.SweepFailed(logger, error);
                wait = FailureInterval;
            }

            try
            {
                await Task.Delay(wait, clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await store.TakeDueAsync(now, now + SeamlessResender.Lease, Batch, ct).ConfigureAwait(false);
        foreach (var pending in due)
        {
            await resender.ResendAsync(pending, ct).ConfigureAwait(false);
        }
    }
}
