using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;

namespace Apogeu.Wallet.Infrastructure.Demo;

#pragma warning disable MA0025 // Scaffold of SPEC-0021 (no logic yet): implemented in the green phase.

/// <summary>
/// Demo adapter of <see cref="IOperatorWallet"/> (SPEC-0021 §2): the ledger is the player's balance, which opens at
/// 3.000,00 per player, operator and currency without an entry; recharge back to it only below the minimum bet of the
/// currency (<see cref="IOperatorLimits"/>).
/// </summary>
public sealed class DemoWallet : IOperatorWallet, IDemoWallet
{
    public DemoWallet(IWalletLedger ledger, IOperatorLimits limits, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(clock);
    }

    public Task<WalletResult> DebitAsync(WalletOperation op, CancellationToken ct) => throw new NotImplementedException();

    public Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct) => throw new NotImplementedException();

    public Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct) => throw new NotImplementedException();

    public Task<Balance> GetBalanceAsync(PlayerRef player, Currency currency, CancellationToken ct) => throw new NotImplementedException();

    public Task<Balance> RechargeAsync(PlayerRef player, Currency currency, CancellationToken ct) => throw new NotImplementedException();
}
#pragma warning restore MA0025
