namespace Apogeu.Betting.Features.Wallet;

/// <summary>The wallet did not confirm a reversal yet: Wolverine retries the <see cref="ReverseBetDebit"/> later.</summary>
public sealed class WalletReversalPendingException : Exception
{
    public WalletReversalPendingException()
    {
    }

    public WalletReversalPendingException(string message)
        : base(message)
    {
    }

    public WalletReversalPendingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
