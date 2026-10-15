namespace Apogeu.DemoCasino.Wallet;

/// <summary>
/// The casino's own wallet (SPEC-0047 §6), in the memory of the process: one balance per player and currency, opened with
/// R$ 3.000,00 on first use (Emenda v1: the players of the casino and of /dev/launch-demo alike). Every operation is applied
/// once per <c>transactionId</c>: a repetition answers DUPLICATE with the balance right after the original operation. A
/// rollback gives back exactly the debit it names; a rollback without that debit is TRANSACTION_NOT_FOUND and the debit, if
/// it arrives late, is never applied (contract §5). One lock serializes every operation, so two debits of the same player
/// never take the balance below zero and two calls with the same transactionId have one effect.
/// </summary>
internal sealed class CasinoWallet
{
    public const decimal OpeningBalance = 3_000.00m;

    private readonly Lock _gate = new();
    private readonly Dictionary<(string PlayerId, string Currency), decimal> _balances = [];
    private readonly Dictionary<string, Applied> _applied = new(StringComparer.Ordinal);
    private readonly HashSet<string> _voidedDebits = new(StringComparer.Ordinal);

    public WalletOutcome Balance(string playerId, string currency)
    {
        lock (_gate)
        {
            return new WalletOutcome(WalletStatus.Ok, BalanceOf((playerId, currency)));
        }
    }

    public WalletOutcome Debit(string transactionId, string playerId, string currency, decimal amount)
    {
        lock (_gate)
        {
            if (_applied.TryGetValue(transactionId, out var original))
            {
                return new WalletOutcome(WalletStatus.Duplicate, original.BalanceAfter);
            }

            var account = (playerId, currency);
            var balance = BalanceOf(account);

            // A debit already rolled back (its rollback came first) is never applied: nothing is debited.
            if (_voidedDebits.Contains(transactionId) || amount > balance)
            {
                return new WalletOutcome(WalletStatus.InsufficientFunds, balance);
            }

            return Apply(transactionId, account, -amount, debitOf: null);
        }
    }

    public WalletOutcome Credit(string transactionId, string playerId, string currency, decimal amount)
    {
        lock (_gate)
        {
            return _applied.TryGetValue(transactionId, out var original)
                ? new WalletOutcome(WalletStatus.Duplicate, original.BalanceAfter)
                : Apply(transactionId, (playerId, currency), amount, debitOf: null);
        }
    }

    public WalletOutcome Rollback(string transactionId, string originalTransactionId, string playerId, string currency)
    {
        lock (_gate)
        {
            if (_applied.TryGetValue(transactionId, out var original))
            {
                return new WalletOutcome(WalletStatus.Duplicate, original.BalanceAfter);
            }

            if (!_applied.TryGetValue(originalTransactionId, out var debit) || debit.Kind != Kind.Debit)
            {
                _voidedDebits.Add(originalTransactionId);
                return new WalletOutcome(WalletStatus.TransactionNotFound, BalanceOf((playerId, currency)));
            }

            // Already given back by another rollback: never twice.
            if (debit.RolledBackBy is { } refund)
            {
                return new WalletOutcome(WalletStatus.Duplicate, _applied[refund].BalanceAfter);
            }

            var outcome = Apply(transactionId, debit.Account, -debit.Amount, debitOf: originalTransactionId);
            _applied[originalTransactionId] = debit with { RolledBackBy = transactionId };
            return outcome;
        }
    }

    private decimal BalanceOf((string PlayerId, string Currency) account)
    {
        if (!_balances.TryGetValue(account, out var balance))
        {
            balance = OpeningBalance;
            _balances[account] = balance;
        }

        return balance;
    }

    /// <summary>Applies <paramref name="amount"/> (negative for a debit) and records it under <paramref name="transactionId"/>.</summary>
    private WalletOutcome Apply(string transactionId, (string PlayerId, string Currency) account, decimal amount, string? debitOf)
    {
        var after = BalanceOf(account) + amount;
        _balances[account] = after;
        var kind = debitOf is not null ? Kind.Rollback : amount < 0 ? Kind.Debit : Kind.Credit;
        _applied[transactionId] = new Applied(kind, account, amount, after, RolledBackBy: null);
        return new WalletOutcome(WalletStatus.Ok, after);
    }

    private enum Kind
    {
        Debit,
        Credit,
        Rollback,
    }

    /// <summary>An applied operation: its account, the signed amount and the balance right after it.</summary>
    private sealed record Applied(Kind Kind, (string PlayerId, string Currency) Account, decimal Amount, decimal BalanceAfter, string? RolledBackBy);
}
