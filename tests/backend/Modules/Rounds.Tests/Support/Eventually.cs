using System.Diagnostics;
using Xunit;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>Polls a condition on the real clock (integration tests only).</summary>
internal static class Eventually
{
    /// <summary>Waits until <paramref name="condition"/> holds and returns the elapsed time; fails after <paramref name="timeout"/>.</summary>
    public static async Task<TimeSpan> HoldsAsync(Func<bool> condition, TimeSpan timeout, Func<string> describe)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(describe);
        var ct = TestContext.Current.CancellationToken;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (condition())
            {
                return watch.Elapsed;
            }

            await Task.Delay(20, ct).ConfigureAwait(false);
        }

        Assert.Fail($"Not within {timeout}: {describe()}");
        return watch.Elapsed;
    }

    public static async Task<T> ResultAsync<T>(Func<Task<T?>> probe, Func<T, bool> accept, TimeSpan timeout, Func<string> describe)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(accept);
        ArgumentNullException.ThrowIfNull(describe);
        var ct = TestContext.Current.CancellationToken;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            var value = await probe().ConfigureAwait(false);
            if (value is not null && accept(value))
            {
                return value;
            }

            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        Assert.Fail($"Not within {timeout}: {describe()}");
        return null!;
    }
}
