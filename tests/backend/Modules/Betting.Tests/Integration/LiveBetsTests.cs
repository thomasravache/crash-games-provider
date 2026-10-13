using System.Text.Json;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// IT-01 (SPEC-0025 §6, ADR-0005): the live bets of the room on the real Host — two players of different operators (the
/// second one through the <see cref="RivalOperatorLimits"/> double and a session of its own operator), each with a nickname in
/// the claim <c>nick</c>; the snapshot of <c>JoinSalaBets</c> and the <c>LiveBetsBatch</c> of the room's group are
/// anonymized, carry an opaque <c>betRef</c> and never a bet id, a full nickname or a player id.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class LiveBetsTests(BettingContainers containers)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "Dados dois jogadores de operadores diferentes na mesma sala, quando um aposta e retira e o outro entra no meio da rodada, então o segundo recebe snapshot e lotes anonimizados, com betRef opaco e sem betId")]
    [Trait("Spec", "SPEC-0025:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenPlayersOfTwoOperatorsWhenOneBetsAndCashesOutAndTheOtherJoinsMidRoundThenSnapshotAndBatchesAreAnonymized()
    {
        var ct = TestContext.Current.CancellationToken;

        // 8 s of bets so the second player joins and bets in the same betting phase; every flight crashes at 2,00x (≈ 3,5 s).
        await using var host = await BettingHost.StartAsync(
            containers,
            ct,
            services: services =>
            {
                FixedCrashFairness.Register(services, 2.00m);
                RivalOperatorLimits.Register(services);
            },
            sala: new BettingSala(BettingDuration: "00:00:08"));
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var (marcosId, beatrizId) = ("player-" + Guid.NewGuid().ToString("N"), "player-" + Guid.NewGuid().ToString("N"));
        await using var marcos = await BetsClient.ConnectAsync(host, marcosId, ct, nickname: "Marcos");
        var joined = await marcos.JoinSalaBetsAsync(ct);
        Assert.True(joined.Error is null && joined.Result is { ValueKind: JsonValueKind.Object }, $"JoinSalaBets of the first player: {joined}");

        // The first player (operator demo) bets 10,00 and 25,00 in the betting phase.
        var betting = await rounds.NextBettingAsync(TimeSpan.FromSeconds(5));
        var ten = BetsClient.BetId(await marcos.PlaceAsync(panel: 1, amount: "10.00", ct));
        var twentyFive = BetsClient.BetId(await marcos.PlaceAsync(panel: 2, amount: "25.00", ct));

        // The second player (another operator) joins in the middle of the round: the snapshot has the current bets.
        await using var beatriz = await BetsClient.ConnectAsync(host, beatrizId, ct, nickname: "Beatriz", operatorId: RivalOperatorLimits.Operator);
        var first = await SnapshotAsync(beatriz, s => s.GetProperty("totalBets").GetInt32() == 2);
        var tenRef = AssertFirstSnapshot(first, betting.RoundId);

        // The second player bets 15,00: both players receive it in a batch of the room's group, anonymized.
        var fifteen = await AssertBroadcastOfFifteenAsync(beatriz, marcos, betting.RoundId, ct);

        // The flight: the first player cashes out the 10,00 at ≈ 1 s; the second receives it with the betRef of the snapshot.
        var flight = CashOutFlight.Of(await rounds.NextFlightAsync());
        Assert.Equal(betting.RoundId, flight.RoundId);
        await host.WaitActiveAsync([ten, twentyFive, fifteen], ct);
        await flight.AtAsync(TimeSpan.FromSeconds(1), ct);
        var cashedOut = CashOutFlight.CashedOutOf(await marcos.CashOutAsync(ten, ct));
        var (multiplier, payout) = (Json.Decimal(cashedOut, "multiplier"), Json.Decimal(cashedOut, "payout"));
        var withCashOut = await beatriz.WaitForLiveBatchAsync(b => Items(b, "cashedOut").Any(c => IsRef(c, tenRef)), Timeout);
        var liveCashOut = Items(withCashOut.Payload, "cashedOut").Single(c => IsRef(c, tenRef));
        Assert.Equal(multiplier, Json.Decimal(liveCashOut, "multiplier"));
        Assert.Equal(payout, Json.Decimal(liveCashOut, "payout"));
        Assert.Equal(flight.RoundId, withCashOut.RoundId);

        // A reconnection of the second player receives the current snapshot, with the cash-out on the bet.
        await using var reconnected = await BetsClient.ConnectAsync(host, beatrizId, ct, nickname: "Beatriz", operatorId: RivalOperatorLimits.Operator);
        var current = await SnapshotAsync(reconnected, s => Items(s, "bets").Any(b => !Json.IsNullOrMissing(b, "multiplier")));
        AssertReconnectionSnapshot(current, flight.RoundId, tenRef, multiplier, payout);

        // Nothing internal reaches the room: no betId, no full nickname, no player id; every betRef is opaque.
        var payloads = new List<(string Context, JsonElement Payload)> { ("snapshot", first), ("reconnection snapshot", current) };
        payloads.AddRange(marcos.LiveBatches.Concat(beatriz.LiveBatches).Concat(reconnected.LiveBatches).Select(b => ("LiveBetsBatch", b.Payload)));
        AssertNothingInternal(payloads, [ten, twentyFive, fifteen], [marcosId, beatrizId]);
    }

    /// <summary>The snapshot of the second player: the two bets of the first one, highest first, anonymized; the betRef of the 10,00.</summary>
    private static string AssertFirstSnapshot(JsonElement snapshot, Guid roundId)
    {
        Assert.Equal(roundId, snapshot.GetProperty("roundId").GetGuid());
        Assert.Equal(1, snapshot.GetProperty("totalPlayers").GetInt32());
        Assert.Equal(35.00m, Json.Decimal(snapshot, "totalWagered"));
        var bets = Items(snapshot, "bets");
        Assert.Equal([25.00m, 10.00m], bets.Select(b => Json.Decimal(b, "amount")));
        Assert.All(bets, b =>
        {
            Assert.Equal("Ma***", Json.Text(b, "nickname"));
            Assert.Equal("BRL", Json.Text(b, "currency"));
            Assert.True(Json.IsNullOrMissing(b, "multiplier"), $"A bet still in the round has no multiplier: {b.GetRawText()}");
            Assert.True(Json.IsNullOrMissing(b, "payout"), $"A bet still in the round has no payout: {b.GetRawText()}");
        });
        var tenRef = Json.Text(bets[1], "betRef");
        Assert.False(IsRef(bets[0], tenRef), $"Each bet has its own betRef: {snapshot.GetRawText()}");
        return tenRef;
    }

    /// <summary>The second player bets 15,00; both players receive it in a <c>LiveBetsBatch</c> with the round totals.</summary>
    private static async Task<Guid> AssertBroadcastOfFifteenAsync(BetsClient beatriz, BetsClient marcos, Guid roundId, CancellationToken ct)
    {
        var fifteen = BetsClient.BetId(await beatriz.PlaceAsync(panel: 1, amount: "15.00", ct).ConfigureAwait(false));
        foreach (var player in (BetsClient[])[beatriz, marcos])
        {
            var batch = await player.WaitForLiveBatchAsync(b => Items(b, "placed").Any(IsFifteen), Timeout).ConfigureAwait(false);
            Assert.Equal("Be***", Json.Text(Items(batch.Payload, "placed").Single(IsFifteen), "nickname"));
            Assert.Equal(roundId, batch.RoundId);
            Assert.Equal(3, batch.Payload.GetProperty("totalBets").GetInt32());
            Assert.Equal(2, batch.Payload.GetProperty("totalPlayers").GetInt32());
            Assert.Equal(50.00m, Json.Decimal(batch.Payload, "totalWagered"));
        }

        return fifteen;

        static bool IsFifteen(JsonElement bet) => Json.Decimal(bet, "amount") == 15.00m;
    }

    private static void AssertReconnectionSnapshot(JsonElement snapshot, Guid roundId, string tenRef, decimal multiplier, decimal payout)
    {
        Assert.Equal(roundId, snapshot.GetProperty("roundId").GetGuid());
        Assert.Equal(3, snapshot.GetProperty("totalBets").GetInt32());
        Assert.Equal(2, snapshot.GetProperty("totalPlayers").GetInt32());
        Assert.Equal(50.00m, Json.Decimal(snapshot, "totalWagered"));
        var bets = Items(snapshot, "bets");
        Assert.Equal([25.00m, 15.00m, 10.00m], bets.Select(b => Json.Decimal(b, "amount")));
        Assert.Equal(["Ma***", "Be***", "Ma***"], bets.Select(b => Json.Text(b, "nickname")), StringComparer.Ordinal);
        Assert.True(IsRef(bets[2], tenRef), $"The 10,00 keeps its betRef: {snapshot.GetRawText()}");
        Assert.Equal(multiplier, Json.Decimal(bets[2], "multiplier"));
        Assert.Equal(payout, Json.Decimal(bets[2], "payout"));
    }

    private static void AssertNothingInternal(List<(string Context, JsonElement Payload)> payloads, Guid[] betIds, string[] players)
    {
        foreach (var (context, payload) in payloads)
        {
            LiveBetsPrivacy.AssertNothingLeaks(payload, betIds, ["Marcos", "Beatriz"], players, context);
            foreach (var bet in Items(payload, "bets").Concat(Items(payload, "placed")))
            {
                LiveBetsPrivacy.AssertAnonymized(Json.Text(bet, "nickname"), context);
                LiveBetsPrivacy.AssertOpaqueRef(Json.Text(bet, "betRef"), betIds, context);
            }

            foreach (var cash in Items(payload, "cashedOut"))
            {
                LiveBetsPrivacy.AssertOpaqueRef(Json.Text(cash, "betRef"), betIds, context);
            }
        }
    }

    /// <summary>
    /// The snapshot of <c>JoinSalaBets</c> once it satisfies <paramref name="accept"/> (the projection is fed by the bet
    /// events, so it may lag the hub result of a bet by a moment).
    /// </summary>
    private static async Task<JsonElement> SnapshotAsync(BetsClient client, Func<JsonElement, bool> accept)
    {
        var ct = TestContext.Current.CancellationToken;
        var outcome = await Eventually.ResultAsync(
            async () => await client.JoinSalaBetsAsync(ct).ConfigureAwait(false),
            o => o.Result is { } snapshot && accept(snapshot),
            Timeout,
            () => $"the snapshot of JoinSalaBets; got {client.Outcomes.LastOrDefault(o => string.Equals(o.Method, "JoinSalaBets", StringComparison.Ordinal))}").ConfigureAwait(false);
        return outcome.Value;
    }

    private static bool IsRef(JsonElement item, string betRef) => string.Equals(Json.Text(item, "betRef"), betRef, StringComparison.Ordinal);

    /// <summary>The array <paramref name="property"/> of a snapshot or batch (empty when absent).</summary>
    private static List<JsonElement> Items(JsonElement payload, string property) =>
        payload.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array ? [.. array.EnumerateArray()] : [];
}
