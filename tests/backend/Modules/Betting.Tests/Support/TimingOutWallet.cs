using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// The wallet of IT-03 that times out after debiting (SPEC-0022 §7.6): every debit is applied by the inner
/// <see cref="FakeOperatorWallet"/> but answered <see cref="WalletUnavailable"/> (retryable), so the caller cannot know it
/// happened; the first rollback attempt is also <see cref="WalletUnavailable"/> without effect, the next ones reach the
/// inner wallet (ADR-0007: the automatic reversal is retried until confirmed).
/// </summary>
internal sealed class TimingOutWallet(FakeOperatorWallet inner) : IOperatorWallet
{
    private int _rollbackAttempts;

    public FakeOperatorWallet Inner { get; } = inner;

    /// <summary>How many times a rollback was attempted, including the one refused.</summary>
    public int RollbackAttempts => Volatile.Read(ref _rollbackAttempts);

    public async Task<WalletResult> DebitAsync(WalletOperation op, CancellationToken ct)
    {
        await Inner.DebitAsync(op, ct).ConfigureAwait(false);
        return new WalletUnavailable(Retryable: true);
    }

    public Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct) => Inner.CreditAsync(op, ct);

    public Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct) =>
        Interlocked.Increment(ref _rollbackAttempts) == 1
            ? Task.FromResult<WalletResult>(new WalletUnavailable(Retryable: true))
            : Inner.RollbackAsync(op, ct);

    public Task<Balance> GetBalanceAsync(PlayerRef player, Currency currency, CancellationToken ct) => Inner.GetBalanceAsync(player, currency, ct);
}
