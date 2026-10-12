using Apogeu.Betting.Contracts;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Support;

/// <summary>Waits of the settlement integration tests (SPEC-0024): bet statuses in <c>betting.bets</c> and the round summaries.</summary>
internal static class Settlements
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Waits until the bet <paramref name="betId"/> is <paramref name="status"/> (<c>lost</c>, <c>refunded</c>, <c>cashed_out</c>…).</summary>
    public static async Task WaitStatusAsync(BettingHost host, Guid betId, string status, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        string? current = null;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.Elapsed < Timeout)
        {
            current = await host.BetStatusAsync(betId, ct).ConfigureAwait(false);
            if (string.Equals(current, status, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(50, ct).ConfigureAwait(false);
        }

        Assert.Fail($"Bet {betId} is '{current}', not '{status}', after {Timeout}.");
    }

    /// <summary>Waits for the summaries of <paramref name="roundId"/> (one per operator and currency).</summary>
    public static Task<IReadOnlyList<RoundSettlement>> WaitSummaryAsync(BettingHost host, Guid roundId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        return Eventually.ResultAsync(
            async () => await host.SettlementsAsync(roundId, ct).ConfigureAwait(false),
            summaries => summaries.Count > 0,
            Timeout,
            () => $"the settlement summary of round {roundId}");
    }

    /// <summary>The single summary of <paramref name="roundId"/> (operator <c>demo</c>, BRL), checked against its own GGR rule.</summary>
    public static async Task<RoundSettlement> SingleSummaryAsync(BettingHost host, Guid roundId, CancellationToken ct)
    {
        var summary = Assert.Single(await WaitSummaryAsync(host, roundId, ct).ConfigureAwait(false));
        Assert.Equal(roundId, summary.RoundId.Value);
        Assert.Equal(BettingSessions.Operator, summary.OperatorId.Value);
        Assert.Equal(BettingSessions.Currency, summary.Currency.Code);
        Assert.Equal(summary.TotalWagered.Value - summary.TotalPaid.Value - summary.TotalRefunded.Value, summary.Ggr.Value);
        return summary;
    }
}
