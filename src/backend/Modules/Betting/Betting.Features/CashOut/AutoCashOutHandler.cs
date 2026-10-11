using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.Rounds.Contracts;
using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features.CashOut;

/// <summary>
/// Handles <see cref="AutoCashOutExecuted"/> (SPEC-0023 §6), delivered in order with the round events: the leader executed the
/// automatic cash-out of the bet at its exact target, so Betting cashes it out, credits it and sends <c>CashedOut{auto:true}</c>.
/// </summary>
public static class AutoCashOutHandler
{
    public static async Task HandleAsync(
        AutoCashOutExecuted executed,
        IBetStore store,
        CashOutSettlement settlement,
        BetsNotifier notifier,
        ILogger<AutoCashOutExecuted> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(executed);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(notifier);

        if (await store.FindByIdAsync(executed.BetId, ct).ConfigureAwait(false) is not { } found)
        {
            BettingLog.AutoCashOutIgnored(logger, executed.BetId, "unknown");
            return;
        }

        if (found.Bet.Status != BetStatus.Active || found.Bet.RoundId != executed.RoundId)
        {
            // Cashed out manually first (or not in this round's flight): the cash-out it has stands.
            BettingLog.AutoCashOutIgnored(logger, executed.BetId, found.Bet.Status.ToString());
            return;
        }

        if (await settlement.SettleAsync(found, executed.Multiplier.Value, automatic: true).ConfigureAwait(false) is not { Auto: true } cashedOut)
        {
            return;
        }

        try
        {
            await notifier.CashedOutAsync(found.Bet.Owner, cashedOut).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The cash-out is settled; only the message to the player failed (a new CashOut answers it).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BettingLog.CashOutNotSent(logger, executed.BetId, exception);
        }
    }
}
