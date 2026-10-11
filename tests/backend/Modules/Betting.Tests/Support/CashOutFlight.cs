using System.Globalization;
using System.Text.Json;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Support;

/// <summary>The flight of a round as the players see it on <c>/hubs/rounds</c> (SPEC-0008): its id, start and growth constant.</summary>
internal sealed record CashOutFlight(Guid RoundId, DateTimeOffset StartedAt, double K)
{
    /// <summary>
    /// Waits for a betting phase with at least 1,5 s left, places the bets of <paramref name="place"/> in it, then waits for
    /// their flight and until every one of them is <c>active</c> in Betting.
    /// </summary>
    public static async Task<(CashOutFlight Flight, IReadOnlyList<Guid> Bets)> PlaceAndFlyAsync(
        BettingHost host,
        RoundWatch rounds,
        Func<Task<IReadOnlyList<Guid>>> place,
        CancellationToken ct,
        TimeSpan? bettingLeft = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(rounds);
        ArgumentNullException.ThrowIfNull(place);
        var betting = await rounds.NextBettingAsync(bettingLeft ?? TimeSpan.FromSeconds(1.5)).ConfigureAwait(false);
        var bets = await place().ConfigureAwait(false);
        var flying = await rounds.NextFlightAsync().ConfigureAwait(false);
        Assert.Equal(betting.RoundId, flying.RoundId);
        await host.WaitActiveAsync(bets, ct).ConfigureAwait(false);
        return (Of(flying), bets);
    }

    public static CashOutFlight Of(HubEvent flying)
    {
        ArgumentNullException.ThrowIfNull(flying);
        return new CashOutFlight(
            flying.RoundId,
            Json.Instant(flying.Payload, "startedAt"),
            double.Parse(Json.Text(flying.Payload, "growthK"), CultureInfo.InvariantCulture));
    }

    /// <summary>Waits (real clock) until <paramref name="elapsed"/> of flight.</summary>
    public async Task AtAsync(TimeSpan elapsed, CancellationToken ct)
    {
        var wait = StartedAt + elapsed - DateTimeOffset.UtcNow;
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, ct).ConfigureAwait(false);
        }
    }

    /// <summary>The multiplier of a manual cash-out received at <paramref name="at"/> (server and test share the clock).</summary>
    public decimal MultiplierAt(DateTimeOffset at) => CashOutMath.MultiplierAt(K, at - StartedAt);

    /// <summary>
    /// Asserts the multiplier of a manual cash-out sent at <paramref name="sentAt"/> and answered at <paramref name="answeredAt"/>:
    /// the one of an instant of reception between the two (startedAt travels in whole milliseconds, hence 1 ms of slack).
    /// </summary>
    public void AssertReceivedBetween(decimal multiplier, DateTimeOffset sentAt, DateTimeOffset answeredAt, string context)
    {
        var lowest = MultiplierAt(sentAt - TimeSpan.FromMilliseconds(1));
        var highest = MultiplierAt(answeredAt + TimeSpan.FromMilliseconds(1));
        Assert.True(
            multiplier >= lowest && multiplier <= highest,
            string.Create(CultureInfo.InvariantCulture, $"The multiplier {multiplier} is floor(e^(k·(receivedAt − startedAt))·100)/100 for a reception between {sentAt:O} and {answeredAt:O} (from {lowest} to {highest}; startedAt {StartedAt:O}, k {K}). {context}"));
    }

    /// <summary>The <c>cashedOut</c> member of a <c>CashOutResult</c>; fails the test when the result is not a cash-out.</summary>
    public static JsonElement CashedOutOf(HubOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var result = outcome.Value;
        Assert.True(
            result.TryGetProperty("cashedOut", out var cashedOut) && cashedOut.ValueKind == JsonValueKind.Object,
            $"Expected CashOutResult.cashedOut, got {outcome}");
        Assert.True(Json.IsNullOrMissing(result, "pending"), $"CashOutResult has exactly one member set: {outcome}");
        return cashedOut;
    }

    /// <summary>The <c>pending</c> member of a <c>CashOutResult</c>; fails the test when the result is not pending.</summary>
    public static JsonElement PendingOf(HubOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var result = outcome.Value;
        Assert.True(
            result.TryGetProperty("pending", out var pending) && pending.ValueKind == JsonValueKind.Object,
            $"Expected CashOutResult.pending, got {outcome}");
        Assert.True(Json.IsNullOrMissing(result, "cashedOut"), $"CashOutResult has exactly one member set: {outcome}");
        return pending;
    }
}
