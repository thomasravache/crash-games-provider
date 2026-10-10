using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Wallet.Contracts;

/// <summary>The demo balance is not below the minimum bet, so it cannot be recharged (<see cref="WalletErrorCodes.RechargeNotAllowed"/>).</summary>
public sealed class RechargeNotAllowedException : Exception
{
    public RechargeNotAllowedException()
        : base("The demo balance still covers the minimum bet.")
    {
    }

    public RechargeNotAllowedException(string message)
        : base(message)
    {
    }

    public RechargeNotAllowedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public RechargeNotAllowedException(PlayerRef player, Balance balance)
        : base("The demo balance still covers the minimum bet.")
    {
        Player = player;
        Balance = balance;
    }

    /// <summary>The contract error code, <c>RECHARGE_NOT_ALLOWED</c>.</summary>
    public string Code { get; } = WalletErrorCodes.RechargeNotAllowed;

    public PlayerRef? Player { get; }

    /// <summary>The balance that was not recharged.</summary>
    public Balance? Balance { get; }
}
