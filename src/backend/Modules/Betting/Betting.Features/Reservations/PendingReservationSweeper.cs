using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apogeu.Betting.Features.Reservations;

/// <summary>
/// Voids the bet reservations whose debit was never confirmed (SPEC-0022 emenda 3): every instance sweeps the pending
/// reservations older than <see cref="BettingOptions.PendingTimeout"/> and voids each one together with the durable reversal
/// of its debit. Voiding is conditional on the reservation still being pending, so a bet confirmed meanwhile (or voided by
/// another instance) is left alone and each debit is reversed once.
/// </summary>
internal sealed class PendingReservationSweeper(
    IServiceScopeFactory scopes,
    IMessagingReadiness messaging,
    IOptions<BettingOptions> options,
    TimeProvider time,
    ILogger<PendingReservationSweeper> logger) : BackgroundService
{
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The reversals are written through the outbox: nothing is swept before the messaging runs.
            await messaging.WhenReadyAsync(stoppingToken).ConfigureAwait(false);
            while (!stoppingToken.IsCancellationRequested)
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(options.Value.SweepInterval, time, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host stops.
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            var scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var store = scope.ServiceProvider.GetRequiredService<IBetStore>();
                var stale = await store.ListStalePendingAsync(time.GetUtcNow() - options.Value.PendingTimeout, BatchSize, ct).ConfigureAwait(false);
                foreach (var betId in stale)
                {
                    if (await store.VoidAsync(betId, ct).ConfigureAwait(false))
                    {
                        BettingLog.ReservationSwept(logger, betId);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            BettingLog.SweepFailed(logger, exception);
        }
    }
}
