using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// In-memory double of <see cref="IOperatorWallet"/> built only from the contract v2 of SPEC-0021 §6: balance per player and
/// currency opening at 3.000,00 (the demo opening balance) unless the test sets another; every operation idempotent by
/// <see cref="TransactionId"/> — a repeated id returns <see cref="WalletDuplicate"/> with the original result and changes
/// nothing (20 equal debits → one <see cref="WalletOk"/> and 19 duplicates); a debit above the balance is
/// <see cref="WalletInsufficientFunds"/> with nothing posted; a rollback reverses the debit of its
/// <c>OriginalTransactionId</c>, or is <see cref="WalletTransactionNotFound"/> (final) when there is none. Records every call.
/// </summary>
internal sealed class FakeOperatorWallet : IOperatorWallet
{
    public const decimal OpeningBalance = 3000.00m;

    private readonly Lock _gate = new();
    private readonly Dictionary<(PlayerRef Player, Currency Currency), decimal> _balances = [];
    private readonly Dictionary<TransactionId, WalletResult> _results = [];
    private readonly Dictionary<TransactionId, WalletOperation> _debits = [];
    private readonly List<WalletCall> _calls = [];

    public IReadOnlyList<WalletCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>Applied debits (first <see cref="WalletOk"/> of a debit transaction id).</summary>
    public IReadOnlyList<WalletCall> AppliedDebits => [.. Calls.Where(c => string.Equals(c.Kind, "debit", StringComparison.Ordinal) && c.Applied)];

    /// <summary>Applied credits (first <see cref="WalletOk"/> of a credit transaction id).</summary>
    public IReadOnlyList<WalletCall> AppliedCredits => [.. Calls.Where(c => string.Equals(c.Kind, "credit", StringComparison.Ordinal) && c.Applied)];

    /// <summary>Applied rollbacks (first <see cref="WalletOk"/> of a rollback transaction id).</summary>
    public IReadOnlyList<WalletCall> AppliedRollbacks => [.. Calls.Where(c => string.Equals(c.Kind, "rollback", StringComparison.Ordinal) && c.Applied)];

    /// <summary>Sets the balance of a player before the test bets (instead of the 3.000,00 demo opening).</summary>
    public void SetBalance(PlayerRef player, Currency currency, decimal amount)
    {
        lock (_gate)
        {
            _balances[(player, currency)] = amount;
        }
    }

    public decimal BalanceOf(PlayerRef player, Currency currency)
    {
        lock (_gate)
        {
            return Current(player, currency);
        }
    }

    public Task<WalletResult> DebitAsync(WalletOperation op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        lock (_gate)
        {
            WalletResult result;
            if (_results.TryGetValue(op.TransactionId, out var original))
            {
                result = new WalletDuplicate(original);
            }
            else if (op.Amount.Value > Current(op.Player, op.Currency))
            {
                // Nothing posted: the same id may be sent again later.
                result = new WalletInsufficientFunds(new Balance(new Money(Current(op.Player, op.Currency)), op.Currency));
            }
            else
            {
                var after = Current(op.Player, op.Currency) - op.Amount.Value;
                _balances[(op.Player, op.Currency)] = after;
                result = new WalletOk(new Balance(new Money(after), op.Currency));
                _results[op.TransactionId] = result;
                _debits[op.TransactionId] = op;
            }

            _calls.Add(new WalletCall("debit", op.TransactionId, op.Player, op.BetId, op.Amount.Value, null, result));
            return Task.FromResult(result);
        }
    }

    public Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        lock (_gate)
        {
            WalletResult result;
            if (_results.TryGetValue(op.TransactionId, out var original))
            {
                result = new WalletDuplicate(original);
            }
            else
            {
                var after = Current(op.Player, op.Currency) + op.Amount.Value;
                _balances[(op.Player, op.Currency)] = after;
                result = new WalletOk(new Balance(new Money(after), op.Currency));
                _results[op.TransactionId] = result;
            }

            _calls.Add(new WalletCall("credit", op.TransactionId, op.Player, op.BetId, op.Amount.Value, null, result));
            return Task.FromResult(result);
        }
    }

    public Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        lock (_gate)
        {
            WalletResult result;
            if (_results.TryGetValue(op.TransactionId, out var original))
            {
                result = new WalletDuplicate(original);
            }
            else if (!_debits.TryGetValue(op.OriginalTransactionId, out var debit))
            {
                result = new WalletTransactionNotFound(op.OriginalTransactionId);
            }
            else
            {
                var after = Current(debit.Player, debit.Currency) + debit.Amount.Value;
                _balances[(debit.Player, debit.Currency)] = after;
                result = new WalletOk(new Balance(new Money(after), debit.Currency));
                _results[op.TransactionId] = result;
            }

            _calls.Add(new WalletCall("rollback", op.TransactionId, op.Player, op.BetId, null, op.OriginalTransactionId, result));
            return Task.FromResult(result);
        }
    }

    public Task<Balance> GetBalanceAsync(PlayerRef player, Currency currency, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult(new Balance(new Money(Current(player, currency)), currency));
        }
    }

    private decimal Current(PlayerRef player, Currency currency) =>
        _balances.TryGetValue((player, currency), out var balance) ? balance : OpeningBalance;
}
