using Apogeu.Betting.Contracts;

namespace Apogeu.Wallet.Features.Settlement;

/// <summary>
/// Known violation (UT-01): Wallet.Features depends on Betting.Contracts while Betting.Features depends on
/// Wallet.Contracts (PlaceBetBalanceCheck), a cycle between the Betting and Wallet modules (ADR-0001).
/// </summary>
public static class SettlementListener
{
    public static SettleBetCommand Settle(Guid betId) => new(betId);
}
