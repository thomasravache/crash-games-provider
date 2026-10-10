using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// The <see cref="FakeOperatorWallet"/> (SPEC-0021@2 contract) with programmed faults, the only failure seam of IT-08..IT-10
/// and UT-07: <see cref="AfterDebit"/> runs after a debit was applied and before it is answered (a process that stops
/// there, a round that crashes there, a commit that fails there); <see cref="OnRollback"/> may answer a rollback without
/// reaching the wallet (the wallet is down).
/// </summary>
internal sealed class ProgrammableWallet(FakeOperatorWallet inner) : IOperatorWallet
{
    private int _rollbackAttempts;

    public ProgrammableWallet()
        : this(new FakeOperatorWallet())
    {
    }

    public FakeOperatorWallet Inner { get; } = inner;

    /// <summary>Runs after the debit was applied by <see cref="Inner"/> and before its result is returned.</summary>
    public Func<WalletOperation, Task>? AfterDebit { get; set; }

    /// <summary>A non-null result answers the rollback without reaching <see cref="Inner"/> (nothing is posted).</summary>
    public Func<WalletRollback, WalletResult?>? OnRollback { get; set; }

    /// <summary>Every rollback attempt, answered by <see cref="OnRollback"/> or by the wallet.</summary>
    public int RollbackAttempts => Volatile.Read(ref _rollbackAttempts);

    public async Task<WalletResult> DebitAsync(WalletOperation op, CancellationToken ct)
    {
        var result = await Inner.DebitAsync(op, ct).ConfigureAwait(false);
        if (AfterDebit is { } after)
        {
            await after(op).ConfigureAwait(false);
        }

        return result;
    }

    public Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct) => Inner.CreditAsync(op, ct);

    public Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct)
    {
        Interlocked.Increment(ref _rollbackAttempts);
        return OnRollback?.Invoke(op) is { } programmed ? Task.FromResult(programmed) : Inner.RollbackAsync(op, ct);
    }

    public Task<Balance> GetBalanceAsync(PlayerRef player, Currency currency, CancellationToken ct) => Inner.GetBalanceAsync(player, currency, ct);
}
