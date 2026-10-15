using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Operators.Contracts;

/// <summary>
/// Wallet configuration per operator (SPEC-0046 §6, <c>walletMode demo|seamless</c>): an operator without configuration is
/// <see cref="WalletMode.Demo"/>.
/// </summary>
public interface IOperatorWalletSettings
{
    /// <summary>The wallet configuration of <paramref name="op"/>; <see cref="WalletMode.Demo"/> when it has none.</summary>
    Task<OperatorWalletSettings> GetAsync(OperatorId op, CancellationToken ct);
}
