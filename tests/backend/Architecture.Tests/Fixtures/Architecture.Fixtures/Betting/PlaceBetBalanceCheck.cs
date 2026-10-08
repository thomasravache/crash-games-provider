using Apogeu.Wallet.Contracts;

namespace Apogeu.Betting.Features.PlaceBet;

/// <summary>Allowed dependency (UT-01 control): Betting.Features references Wallet.Contracts.</summary>
public static class PlaceBetBalanceCheck
{
    public static WalletBalanceQuery CreateQuery(Guid playerId) => new(playerId);
}
