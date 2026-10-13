namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>Exponential backoff of the resends of a credit or a rollback (SPEC-0046 §6): 1 s, 2 s, 4 s … capped at 5 min.</summary>
internal static class ResendBackoff
{
    public static readonly TimeSpan First = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan Cap = TimeSpan.FromMinutes(5);

    /// <summary>The wait after the failed attempt number <paramref name="attempts"/> (1 = the first one).</summary>
    public static TimeSpan After(int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        var delay = First;
        for (var i = 1; i < attempts && delay < Cap; i++)
        {
            delay += delay;
        }

        return delay < Cap ? delay : Cap;
    }
}
