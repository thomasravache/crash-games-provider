using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Infrastructure.Demo;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>
/// The module's <see cref="IOperatorWallet"/> (SPEC-0046 §4, ADR-0007): per operator, the demo adapter or the seamless one,
/// by its <c>walletMode</c> (<see cref="IOperatorWalletSettings"/>, demo by default). In seamless mode the debit is one
/// attempt; credits and rollbacks go through the durable resend (<see cref="SeamlessResends"/>), which stays on the
/// operator wallet even if the mode changes while they are pending.
/// </summary>
internal sealed class OperatorWalletRouter(
    IOperatorWalletSettings settings,
    DemoWallet demo,
    SeamlessOperatorWallet seamless,
    SeamlessResends resends) : IOperatorWallet
{
    public async Task<WalletResult> DebitAsync(WalletOperation op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        return await IsSeamlessAsync(op.Player.OperatorId, ct).ConfigureAwait(false)
            ? await seamless.DebitAsync(op, ct).ConfigureAwait(false)
            : await demo.DebitAsync(op, ct).ConfigureAwait(false);
    }

    public async Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        return await IsSeamlessAsync(op.Player.OperatorId, ct).ConfigureAwait(false)
            ? await resends.CreditAsync(op, ct).ConfigureAwait(false)
            : await demo.CreditAsync(op, ct).ConfigureAwait(false);
    }

    public async Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        return await IsSeamlessAsync(op.Player.OperatorId, ct).ConfigureAwait(false)
            ? await resends.RollbackAsync(op, ct).ConfigureAwait(false)
            : await demo.RollbackAsync(op, ct).ConfigureAwait(false);
    }

    public async Task<Balance> GetBalanceAsync(PlayerRef player, Currency currency, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        return await IsSeamlessAsync(player.OperatorId, ct).ConfigureAwait(false)
            ? await seamless.GetBalanceAsync(player, currency, ct).ConfigureAwait(false)
            : await demo.GetBalanceAsync(player, currency, ct).ConfigureAwait(false);
    }

    private async Task<bool> IsSeamlessAsync(OperatorId op, CancellationToken ct) =>
        (await settings.GetAsync(op, ct).ConfigureAwait(false)).Mode == WalletMode.Seamless;
}
