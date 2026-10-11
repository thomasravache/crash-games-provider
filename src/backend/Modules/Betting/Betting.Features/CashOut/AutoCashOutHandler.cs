using Apogeu.Betting.Domain;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.Rounds.Contracts;
using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features.CashOut;

/// <summary>
/// Handles <see cref="AutoCashOutExecuted"/> (SPEC-0023 §6), delivered in order with the round events: the leader executed the
/// automatic cash-out of the bet at its exact target, so Betting stores it with its credit, which then tells the player
/// <c>CashedOut{auto:true}</c> (<see cref="CreditCashOutHandler"/>).
/// </summary>
public static class AutoCashOutHandler
{
    public static async Task HandleAsync(
        AutoCashOutExecuted executed,
        IBetStore store,
        CashOutSettlement settlement,
        ILogger<AutoCashOutExecuted> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(executed);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(settlement);

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

        // Only stored here (the round events never wait for the wallet, review G4 M4): its credit runs at once on its own
        // queue and tells the player.
        await settlement.StoreAsync(found.Bet, executed.Multiplier.Value, automatic: true, ct).ConfigureAwait(false);
    }
}
