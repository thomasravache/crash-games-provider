using System.Diagnostics;
using System.Globalization;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Apogeu.Wallet.Infrastructure.Observability;
using Npgsql;

namespace Apogeu.Wallet.Infrastructure.Demo;

/// <summary>
/// Demo adapter of <see cref="IOperatorWallet"/> (SPEC-0021 §2): the ledger is the player's balance, which opens at
/// 3.000,00 per player, operator and currency without an entry; recharge back to it only below the minimum bet of the
/// currency (<see cref="IOperatorLimits"/>).
/// </summary>
/// <remarks>
/// Every entry is decided on the player's sum read just before and appended only if that sum is still the same
/// (<see cref="IWalletLedger.TryAppendAsync"/>); when another entry got in first the decision is taken again on the fresh
/// sum, so two different debits that together exceed the balance never both apply.
/// </remarks>
public sealed class DemoWallet : IOperatorWallet, IDemoWallet
{
    /// <summary>Opening demo balance of every player, operator and currency (SPEC-0021 §2), implicit: no entry.</summary>
    public const decimal OpeningBalance = 3_000.00m;

    /// <summary>Decisions taken again after concurrent entries before an operation gives up (retryable).</summary>
    internal const int MaxAttempts = 32;

    private const string RechargePrefix = "demo-recharge:";

    /// <summary>Smallest amount of the port: one cent.</summary>
    private const decimal MinAmount = 0.01m;

    /// <summary>Decimal places of the amounts of the port and of the ledger.</summary>
    private const int AmountScale = 2;

    private readonly IWalletLedger _ledger;
    private readonly IOperatorLimits _limits;
    private readonly TimeProvider _clock;
    private readonly WalletMetrics? _metrics;

    public DemoWallet(IWalletLedger ledger, IOperatorLimits limits, TimeProvider clock, WalletMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(clock);

        _ledger = ledger;
        _limits = limits;
        _clock = clock;
        _metrics = metrics;
    }

    public Task<WalletResult> DebitAsync(WalletOperation op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        RequireCents(op.Amount.Value, nameof(op));

        return TracedAsync(
            WalletResultNames.Debit,
            op.TransactionId,
            () => PostAsync(Draft(op, LedgerEntryKind.Debit, -op.Amount.Value), refuseBelow: op.Amount.Value, token => DuplicateOfAsync(op.TransactionId, token), ct));
    }

    public Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        RequireCents(op.Amount.Value, nameof(op));

        return TracedAsync(
            WalletResultNames.Credit,
            op.TransactionId,
            () => PostAsync(Draft(op, LedgerEntryKind.Credit, op.Amount.Value), refuseBelow: null, token => DuplicateOfAsync(op.TransactionId, token), ct));
    }

    public Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);

        return TracedAsync(WalletResultNames.Rollback, op.TransactionId, async () =>
        {
            if (await RefundOfAsync(op, original: null, ct).ConfigureAwait(false) is { } duplicate)
            {
                return duplicate;
            }

            var account = LedgerAccount.OfPlayer(op.Player, op.Currency);
            var original = await _ledger.FindAsync(op.OriginalTransactionId, ct).ConfigureAwait(false);
            var debited = original is { Kind: LedgerEntryKind.Debit }
                ? original.Postings.FirstOrDefault(posting => posting.Account == account)
                : null;
            if (debited is null)
            {
                // No debit of this player and currency under that id: nothing to give back, nothing posted.
                return new WalletTransactionNotFound(op.OriginalTransactionId);
            }

            // A debit is refunded once, whatever the id of the rollback: an earlier refund of it is the result.
            if (await RefundOfAsync(op, original, ct).ConfigureAwait(false) is { } refunded)
            {
                return refunded;
            }

            // The refund gives back exactly the debit, which must itself be a valid amount of the port.
            var refund = -debited.Amount.Value;
            RequireCents(refund, nameof(op));

            var draft = new EntryDraft(
                op.TransactionId, LedgerEntryKind.Rollback, op.Player, op.Currency, op.RoundId, op.BetId, op.OriginalTransactionId, refund);
            return await PostAsync(draft, refuseBelow: null, token => RefundOfAsync(op, original, token), ct).ConfigureAwait(false);
        });
    }

    public async Task<Balance> GetBalanceAsync(PlayerRef player, Currency currency, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);

        var sum = await _ledger.SumAsync(LedgerAccount.OfPlayer(player, currency), ct).ConfigureAwait(false);
        return BalanceOf(sum, currency);
    }

    public async Task<Balance> RechargeAsync(PlayerRef player, Currency currency, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);

        var minBet = _limits.Get(player.OperatorId, currency).MinBet.Value;
        var account = LedgerAccount.OfPlayer(player, currency);
        var transactionId = new TransactionId(RechargePrefix + Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture));
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var sum = await _ledger.SumAsync(account, ct).ConfigureAwait(false);
            var balance = BalanceOf(sum, currency);
            if (balance.Amount.Value >= minBet)
            {
                throw new RechargeNotAllowedException(player, balance);
            }

            var draft = new EntryDraft(
                transactionId, LedgerEntryKind.Recharge, player, currency, RoundId: null, BetId: null, OriginalTransactionId: null, OpeningBalance - balance.Amount.Value);
            if (await _ledger.TryAppendAsync(draft.ToEntry(account, OpeningBalance, _clock), sum, ct).ConfigureAwait(false) == LedgerAppendOutcome.Appended)
            {
                _metrics?.DemoRecharged();
                return new Balance(new Money(OpeningBalance), currency);
            }
        }

        throw new InvalidOperationException("The demo balance kept changing during the recharge; try again.");
    }

    /// <summary>
    /// An amount of the port is a whole number of cents, at least 0,01 (SPEC-0021 UT-06): the ledger keeps two decimal places,
    /// so anything finer would be rounded in silence and the balance reported would differ from the postings.
    /// </summary>
    private static void RequireCents(decimal amount, string paramName)
    {
        if (amount < MinAmount || decimal.Round(amount, AmountScale) != amount)
        {
            throw new ArgumentOutOfRangeException(paramName, "The amount of a wallet operation must be a whole number of cents, at least 0.01.");
        }
    }

    /// <summary>Whether <paramref name="error"/> is a transient failure of the ledger's database (connection lost, timeout).</summary>
    private static bool IsTransient(Exception error) =>
        error is NpgsqlException or TimeoutException
        || error.InnerException is NpgsqlException or TimeoutException;

    private static Balance BalanceOf(Money sum, Currency currency) => new(new Money(OpeningBalance + sum.Value), currency);

    private static WalletOk OkOf(LedgerEntry entry) => new(new Balance(entry.BalanceAfter, entry.Currency));

    private static EntryDraft Draft(WalletOperation op, LedgerEntryKind kind, decimal playerAmount) =>
        new(op.TransactionId, kind, op.Player, op.Currency, op.RoundId, op.BetId, OriginalTransactionId: null, playerAmount);

    /// <summary>
    /// Appends the entry of <paramref name="draft"/> on the current balance, unless its transaction id was already recorded
    /// (whose result comes back as <see cref="WalletDuplicate"/>) or the balance is below <paramref name="refuseBelow"/>
    /// (<see cref="WalletInsufficientFunds"/>, nothing posted).
    /// </summary>
    /// <remarks>
    /// A repeated id always gets <see cref="WalletDuplicate"/>, never a fresh decision: the append itself refuses a known id,
    /// and a refusal for funds is only returned after checking that the id is not already recorded. The id is looked up
    /// only in those two cases, so a new operation costs one read of the sum and one append.
    /// </remarks>
    private async Task<WalletResult> PostAsync(
        EntryDraft draft, decimal? refuseBelow, Func<CancellationToken, Task<WalletDuplicate?>> duplicateOf, CancellationToken ct)
    {
        var account = LedgerAccount.OfPlayer(draft.Player, draft.Currency);
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var sum = await _ledger.SumAsync(account, ct).ConfigureAwait(false);
            var balance = BalanceOf(sum, draft.Currency);
            if (balance.Amount.Value < refuseBelow)
            {
                return (WalletResult?)await duplicateOf(ct).ConfigureAwait(false)
                    ?? new WalletInsufficientFunds(balance);
            }

            var entry = draft.ToEntry(account, balance.Amount.Value + draft.PlayerAmount, _clock);
            switch (await _ledger.TryAppendAsync(entry, sum, ct).ConfigureAwait(false))
            {
                case LedgerAppendOutcome.Appended:
                    return OkOf(entry);
                case LedgerAppendOutcome.DuplicateTransaction when await duplicateOf(ct).ConfigureAwait(false) is { } duplicate:
                    return duplicate;
                default:
                    // Another entry of the player got in first: decide again on the fresh sum.
                    continue;
            }
        }

        return new WalletUnavailable(Retryable: true);
    }

    /// <summary><see cref="WalletDuplicate"/> with the result of the entry recorded under <paramref name="transactionId"/>, if any.</summary>
    private async Task<WalletDuplicate?> DuplicateOfAsync(TransactionId transactionId, CancellationToken ct) =>
        await _ledger.FindAsync(transactionId, ct).ConfigureAwait(false) is { } existing ? new WalletDuplicate(OkOf(existing)) : null;

    /// <summary>
    /// <see cref="WalletDuplicate"/> with the refund already recorded for the debit of <paramref name="op"/>: under the
    /// rollback's own id, under any id the ledger indexes by the original debit, or under the deterministic rollback id of
    /// the debited bet (<c>bet:{betId}:rollback</c>) when it reverses that same debit.
    /// </summary>
    private async Task<WalletDuplicate?> RefundOfAsync(WalletRollback op, LedgerEntry? original, CancellationToken ct)
    {
        if (await DuplicateOfAsync(op.TransactionId, ct).ConfigureAwait(false) is { } sameId)
        {
            return sameId;
        }

        if (original is null)
        {
            return null;
        }

        var refund = await _ledger.FindRollbackOfAsync(op.OriginalTransactionId, ct).ConfigureAwait(false);
        if (refund is null && original.BetId is { } betId && TransactionId.ForRollback(betId) is var canonical && canonical != op.TransactionId)
        {
            refund = await _ledger.FindAsync(canonical, ct).ConfigureAwait(false);
        }

        return refund is { Kind: LedgerEntryKind.Rollback } && refund.OriginalTransactionId == op.OriginalTransactionId
            ? new WalletDuplicate(OkOf(refund))
            : null;
    }

    /// <summary>
    /// Runs <paramref name="operation"/> in an activity tagged with its transaction id (never amounts or tokens). A transient
    /// database failure becomes <see cref="WalletUnavailable"/> (retryable: the same id may be resent) and the activity ends
    /// with status Error and the exception recorded (SPEC-0021 UT-07).
    /// </summary>
    private async Task<WalletResult> TracedAsync(string type, TransactionId transactionId, Func<Task<WalletResult>> operation)
    {
        using var activity = WalletMetrics.ActivitySource.StartActivity("wallet." + type, ActivityKind.Internal);
        activity?.SetTag("wallet.transaction_id", transactionId.Value);
        activity?.SetTag("wallet.adapter", "demo");

        WalletResult result;
        try
        {
            result = await operation().ConfigureAwait(false);
        }
        catch (Exception error) when (IsTransient(error))
        {
            activity?.AddException(error);
            activity?.SetStatus(ActivityStatusCode.Error, error.GetType().Name);
            result = new WalletUnavailable(Retryable: true);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            activity?.AddException(error);
            activity?.SetStatus(ActivityStatusCode.Error, error.GetType().Name);
            throw;
        }

        var name = WalletResultNames.Of(result);
        activity?.SetTag("wallet.result", name);
        _metrics?.Operation(type, name);
        return result;
    }

    /// <summary>What an entry records before the balance it leaves is known: <see cref="PlayerAmount"/> on the player, its opposite on the house.</summary>
    private sealed record EntryDraft(
        TransactionId TransactionId,
        LedgerEntryKind Kind,
        PlayerRef Player,
        Currency Currency,
        RoundId? RoundId,
        Guid? BetId,
        TransactionId? OriginalTransactionId,
        decimal PlayerAmount)
    {
        public LedgerEntry ToEntry(LedgerAccount account, decimal balanceAfter, TimeProvider clock) =>
            new(
                TransactionId,
                Kind,
                Player,
                Currency,
                RoundId,
                BetId,
                OriginalTransactionId,
                [
                    new LedgerPosting(account, new Money(PlayerAmount)),
                    new LedgerPosting(LedgerAccount.OfHouse(account.Operator, account.Currency), new Money(-PlayerAmount)),
                ],
                new Money(balanceAfter),
                clock.GetUtcNow());
    }
}
