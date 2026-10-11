using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Wallet.Contracts;

/// <summary>Demo-only operations of the wallet (SPEC-0021 §6, contract v2).</summary>
public interface IDemoWallet
{
    /// <summary>
    /// Brings the demo balance back to the opening amount (3.000,00) when it is below the minimum bet of the currency
    /// (<c>IOperatorLimits</c>, SPEC-0020); otherwise throws <see cref="RechargeNotAllowedException"/> (<c>RECHARGE_NOT_ALLOWED</c>).
    /// </summary>
    Task<Balance> RechargeAsync(PlayerRef player, Currency currency, CancellationToken ct);
}
