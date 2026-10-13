using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.LiveBets;
using Apogeu.Betting.Features.Wallet;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features.CashOut;

/// <summary>
/// Settles a cash-out (SPEC-0023 §6, ADR-0007): the bet becomes cashed out with its payout
/// (<c>min(floor(amount × multiplier × 100)/100, maxWin)</c>) together with the durable credit of the payout (one transaction,
/// Wolverine outbox). The automatic cash-out has precedence: a manual one decided at or above the bet's target is settled at
/// the exact target, as the automatic one (review G4, M3). <see cref="SettleAsync"/> then tries the credit
/// <c>bet:{betId}:credit</c> at once, outside any transaction, and answers <c>credited</c> with the balance when the wallet
/// confirms it, <c>pending</c> otherwise; <see cref="StoreAsync"/> only stores it, with the credit to run at once on its own
/// queue (the round events never wait for the wallet, M4).
/// </summary>
/// <remarks>
/// The <c>live</c> feed of SPEC-0025 is always registered by the module; it is optional so the use case also runs where the
/// room's live bets do not matter (the unit scenarios of the earlier specs build it without it).
/// </remarks>
public sealed class CashOutSettlement(
    IBetStore store,
    IOperatorLimits limits,
    IOperatorWallet wallet,
    BettingMetrics metrics,
    TimeProvider time,
    ILogger<CashOutSettlement> logger,
    LiveBetsFeed? live = null)
{
    /// <summary>When the durable credit runs if the credit tried at once was not confirmed (it then finds nothing to do).</summary>
    public static readonly TimeSpan CreditRetry = TimeSpan.FromSeconds(3);

    /// <summary>Longest wait for the wallet's answer to one credit attempt tried at once.</summary>
    private static readonly TimeSpan CreditAttemptTimeout = TimeSpan.FromSeconds(1);

    /// <summary>The delay before each credit attempt tried at once: the same credit is resent while its outcome is unknown.</summary>
    private static readonly TimeSpan[] CreditResendDelays = [TimeSpan.Zero, TimeSpan.FromMilliseconds(100)];

    /// <summary>
    /// Cashes out the active bet of <paramref name="found"/> at <paramref name="multiplier"/> and credits it. Returns the
    /// <see cref="CashedOut"/> of the bet — the one already persisted when another call cashed it out first — or
    /// <see langword="null"/> when the bet is no longer active.
    /// </summary>
    public async Task<CashedOut?> SettleAsync(AcceptedBet found, decimal multiplier, bool automatic) =>
        (await SettleOnceAsync(found, multiplier, automatic).ConfigureAwait(false)).CashedOut;

    /// <summary>
    /// As <see cref="SettleAsync"/>, also saying whether this call settled it (<c>Settled</c>), so the player is told once. A bet
    /// already marked lost whose cash-out the leader accepted (received before the crash) becomes cashed out while its round's
    /// summary is not closed (SPEC-0024 review G4, M1); otherwise a critical reconciliation alert.
    /// </summary>
    public async Task<(CashedOut? CashedOut, bool Settled)> SettleOnceAsync(AcceptedBet found, decimal multiplier, bool automatic)
    {
        ArgumentNullException.ThrowIfNull(found);
        var bet = found.Bet;
        var revive = bet.Status == BetStatus.Lost;
        if (bet.Status != BetStatus.Active && !revive)
        {
            return (Answer(found), false);
        }

        if (revive)
        {
            bet = Bet.Restore(bet.Id, bet.Owner, new BetRequest(bet.ClientBetId, bet.Panel, bet.Amount, bet.AutoCashOut), BetStatus.Active, bet.RoundId, bet.RoundNumber);
        }

        (multiplier, automatic) = WithPrecedence(bet, multiplier, automatic);
        if (CashOut(bet, multiplier) is not { } accepted)
        {
            return (null, false);
        }

        // Not tied to the caller: once decided, the cash-out is settled even if the connection is gone.
        var at = time.GetUtcNow();
        var stored = revive
            ? await store.ReviveCashOutAsync(bet, at, CreditRetry, CancellationToken.None).ConfigureAwait(false)
            : await store.CashOutAsync(bet, automatic, at, CreditRetry, announce: false, CancellationToken.None).ConfigureAwait(false);
        if (!stored)
        {
            if (revive)
            {
                BettingLog.LateCashOutUnreconciled(logger, bet.Id, "summary closed or bet changed");
            }

            return (await StoredAsync(bet.Id).ConfigureAwait(false), false);
        }

        metrics.CashedOut(automatic);
        live?.CashedOut(bet, accepted.Multiplier, accepted.Payout);
        var balance = await CreditAsync(bet, accepted.Payout).ConfigureAwait(false);
        if (balance is { } confirmed && !await ConfirmAsync(bet.Id, confirmed).ConfigureAwait(false))
        {
            // m8: the wallet confirmed it but the record failed: answered pending; the durable credit records it.
            balance = null;
        }

        if (balance is null)
        {
            metrics.CreditPending(1);
        }

        return (
            new CashedOut(
                bet.Id,
                new Multiplier(accepted.Multiplier),
                new Money(accepted.Payout),
                balance is null ? CreditStatus.Pending : CreditStatus.Credited,
                balance is { } after ? new Money(after) : null,
                automatic),
            true);
    }

    /// <summary>
    /// Stores the cash-out of the active <paramref name="bet"/> at <paramref name="multiplier"/> with its durable credit to run
    /// at once, which also tells the player (<c>CashedOut</c>); no wallet call here. Returns whether this call cashed it out.
    /// </summary>
    public async Task<bool> StoreAsync(Bet bet, decimal multiplier, bool automatic, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bet);
        (multiplier, automatic) = WithPrecedence(bet, multiplier, automatic);
        if (bet.Status != BetStatus.Active || CashOut(bet, multiplier) is not { } accepted)
        {
            return false;
        }

        if (!await store.CashOutAsync(bet, automatic, time.GetUtcNow(), TimeSpan.Zero, announce: true, ct).ConfigureAwait(false))
        {
            return false;
        }

        metrics.CashedOut(automatic);
        metrics.CreditPending(1);
        live?.CashedOut(bet, accepted.Multiplier, accepted.Payout);
        return true;
    }

    /// <summary>The persisted cash-out of <paramref name="found"/>, or <see langword="null"/> when it is not cashed out.</summary>
    public static CashedOut? Answer(AcceptedBet found)
    {
        ArgumentNullException.ThrowIfNull(found);
        return found.CashOut is { } cashOut
            ? new CashedOut(
                found.Bet.Id,
                new Multiplier(cashOut.Multiplier),
                new Money(cashOut.Payout),
                cashOut.Credited ? CreditStatus.Credited : CreditStatus.Pending,
                cashOut.Credited && cashOut.Balance is { } balance ? new Money(balance) : null,
                cashOut.Auto)
            : null;
    }

    /// <summary>The credit of the payout of a bet (SPEC-0021 §6): <c>bet:{betId}:credit</c>, with the round of the bet.</summary>
    public static WalletOperation CreditOf(Guid betId, PlayerRef player, Currency currency, Rounds.Contracts.RoundId roundId, decimal payout) =>
        new(TransactionId.ForCredit(betId), player, currency, new Money(payout), roundId, betId);

    /// <summary>A manual cash-out at or above the bet's automatic target is the automatic one, at the exact target.</summary>
    private static (decimal Multiplier, bool Automatic) WithPrecedence(Bet bet, decimal multiplier, bool automatic) =>
        bet.AutoCashOut is { } target && multiplier >= target ? (target, true) : (multiplier, automatic);

    private BetCashOutAccepted? CashOut(Bet bet, decimal multiplier) =>
        bet.CashOut(multiplier, limits.Get(bet.Owner.OperatorId, bet.Owner.Currency).MaxWin.Value) as BetCashOutAccepted;

    /// <summary>Records the confirmed credit; whether it is recorded (by this call or already).</summary>
    private async Task<bool> ConfirmAsync(Guid betId, decimal balance)
    {
        try
        {
            await store.ConfirmCreditAsync(betId, balance, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
#pragma warning disable CA1031 // The credit is applied; only its record failed, and the durable credit records it (duplicate).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BettingLog.CreditPending(logger, betId, exception.GetType().Name);
            return false;
        }
    }

    private async Task<CashedOut?> StoredAsync(Guid betId) =>
        await store.FindByIdAsync(betId, CancellationToken.None).ConfigureAwait(false) is { } current ? Answer(current) : null;

    /// <summary>
    /// The credit tried at once: the balance after it when the wallet confirms it, <see langword="null"/> otherwise. While its
    /// outcome is unknown the same credit (same transaction id) is sent again (SPEC-0021 caller rule).
    /// </summary>
    private async Task<decimal?> CreditAsync(Bet bet, decimal payout)
    {
        var credit = CreditOf(bet.Id, new PlayerRef(bet.Owner.OperatorId, bet.Owner.PlayerId), bet.Owner.Currency, bet.RoundId!.Value, payout);
        WalletResult? result = null;
        foreach (var delay in CreditResendDelays)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, time, CancellationToken.None).ConfigureAwait(false);
            }

            try
            {
                result = BetTransactions.Original(
                    await wallet.CreditAsync(credit, CancellationToken.None).WaitAsync(CreditAttemptTimeout, time, CancellationToken.None).ConfigureAwait(false));
            }
#pragma warning disable CA1031 // ADR-0007: any failure leaves the credit unconfirmed; the durable credit retries it.
            catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
            {
                BettingLog.CreditPending(logger, bet.Id, exception.GetType().Name);
                result = null;
            }

            if (result is WalletOk ok)
            {
                return ok.BalanceAfter.Amount.Value;
            }

            if (result is WalletUnavailable { Retryable: false })
            {
                // Permanent: the durable credit raises it too; nothing more is tried here (m6).
                BettingLog.CreditRefused(logger, bet.Id);
                break;
            }
        }

        BettingLog.CreditPending(logger, bet.Id, result?.GetType().Name ?? "no answer");
        return null;
    }
}
