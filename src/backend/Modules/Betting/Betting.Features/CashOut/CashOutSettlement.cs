using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.Wallet;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features.CashOut;

/// <summary>
/// Settles a cash-out the leader accepted (SPEC-0023 §6, ADR-0007): the bet becomes cashed out with its payout
/// (<c>min(floor(amount × multiplier × 100)/100, maxWin)</c>) together with the durable credit of the payout (one transaction,
/// Wolverine outbox); then the credit <c>bet:{betId}:credit</c> is tried at once, outside any transaction. The answer says
/// <c>credited</c> with the balance when the wallet confirms it, <c>pending</c> otherwise (the durable credit goes on).
/// </summary>
public sealed class CashOutSettlement(
    IBetStore store,
    IOperatorLimits limits,
    IOperatorWallet wallet,
    BettingMetrics metrics,
    TimeProvider time,
    ILogger<CashOutSettlement> logger)
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
    public async Task<CashedOut?> SettleAsync(AcceptedBet found, decimal multiplier, bool automatic)
    {
        ArgumentNullException.ThrowIfNull(found);
        var bet = found.Bet;
        if (bet.Status != BetStatus.Active)
        {
            return Answer(found);
        }

        var maxWin = limits.Get(bet.Owner.OperatorId, bet.Owner.Currency).MaxWin.Value;
        if (bet.CashOut(multiplier, maxWin) is not BetCashOutAccepted accepted)
        {
            return null;
        }

        // Not tied to the caller: once decided, the cash-out is settled even if the connection is gone.
        if (!await store.CashOutAsync(bet, automatic, time.GetUtcNow(), CreditRetry, CancellationToken.None).ConfigureAwait(false))
        {
            return await StoredAsync(bet.Id).ConfigureAwait(false);
        }

        metrics.CashedOut(automatic);
        var balance = await CreditAsync(bet, accepted.Payout).ConfigureAwait(false);
        if (balance is { } confirmed)
        {
            await store.ConfirmCreditAsync(bet.Id, confirmed, CancellationToken.None).ConfigureAwait(false);
        }
        else
        {
            metrics.CreditPending(1);
        }

        return new CashedOut(
            bet.Id,
            new Multiplier(accepted.Multiplier),
            new Money(accepted.Payout),
            balance is null ? CreditStatus.Pending : CreditStatus.Credited,
            balance is { } after ? new Money(after) : null,
            automatic);
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
                // Permanent: the durable credit raises it (reconciliation); nothing more is tried here.
                break;
            }
        }

        BettingLog.CreditPending(logger, bet.Id, result?.GetType().Name ?? "no answer");
        return null;
    }
}
