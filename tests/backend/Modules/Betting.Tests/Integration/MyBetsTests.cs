using System.Net;
using System.Text.Json;
using Apogeu.Betting.Tests.Support;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Security.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// IT-02 (SPEC-0025 §6): <c>GET /api/v1/me/bets</c> on the real Host with bets in every final state reached through the real
/// flows — a cash-out and a loss in a round that crashes at 2,00x, a cancellation and a refund in a round annulled by the
/// reveal mismatch (SPEC-0008), and a queued bet without a round, seen before it is cancelled. IT-06 (Emenda v4): a limit
/// that is not a number is <c>400 INVALID_LIMIT</c> in the error envelope.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class MyBetsTests(BettingContainers containers)
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Dado um jogador com apostas cashed_out, lost, cancelled e refunded, quando pagina GET /api/v1/me/bets com limit 2, então recebe só as próprias em ordem e o cursor leva à página seguinte; queued sem rodada tem roundId e roundNumber nulos")]
    [Trait("Spec", "SPEC-0025:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenBetsInEveryFinalStateWhenPagingMyBetsByTwoThenOwnBetsInOrderWithWorkingCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        var startedAt = DateTimeOffset.UtcNow;
        var mismatch = new RevealMismatch();
        await using var host = await BettingHost.StartAsync(containers, ct, services: services =>
        {
            FixedCrashFairness.Register(services, 2.00m);
            services.Decorate<IFairnessService>((inner, _) => new AnnullingFairness(inner, mismatch));
        });
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var (me, other) = ("player-" + Guid.NewGuid().ToString("N"), "player-" + Guid.NewGuid().ToString("N"));
        await using var player = await BetsClient.ConnectAsync(host, me, ct, nickname: "Marcos");
        await using var stranger = await BetsClient.ConnectAsync(host, other, ct, nickname: "Beatriz");
        using var client = host.CreatePlayerHttpClient(me, "Marcos");

        var history = await PlayRoundAAsync(host, rounds, player, stranger, ct);
        mismatch.ArmNextReveal();
        history = await PlayAnnulledRoundBAsync(host, rounds, player, client, history, ct);

        // Paging by 2: 5 bets in 3 pages, most recent first, only the player's own.
        var pages = await MyBetsApi.AllPagesAsync(client, 2, ct);
        Assert.Equal([2, 2, 1], pages.Select(p => p.Count));
        var items = pages.SelectMany(p => p).ToList();
        Assert.Equal(
            [history.Queued, history.Refunded, history.Cancelled, history.Lost, history.CashedOut],
            items.Select(i => i.GetProperty("betId").GetGuid()));
        Assert.DoesNotContain(items, i => i.GetProperty("betId").GetGuid() == history.Strangers);
        AssertOrdered(items);
        Assert.All(items, i =>
        {
            var placedAt = Json.Instant(i, "placedAt");
            Assert.True(placedAt >= startedAt.AddSeconds(-1) && placedAt <= DateTimeOffset.UtcNow.AddSeconds(1), $"placedAt is the acceptance instant: {i.GetRawText()}");
            Assert.Equal("BRL", Json.Text(i, "currency"));
        });

        var (a, b) = (history.RoundA, history.RoundB);
        AssertItem(items[0], "cancelled", panel: 1, amount: 5.00m, roundId: null, roundNumber: null);
        AssertItem(items[1], "refunded", panel: 2, amount: 40.00m, b.RoundId, b.RoundNumber);
        AssertItem(items[2], "cancelled", panel: 1, amount: 30.00m, b.RoundId, b.RoundNumber, autoCashOut: 1.50m);
        AssertItem(items[3], "lost", panel: 2, amount: 20.00m, a.RoundId, a.RoundNumber);
        AssertItem(items[4], "cashed_out", panel: 1, amount: 10.00m, a.RoundId, a.RoundNumber, multiplier: history.Multiplier, payout: history.Payout);

        // The other player sees only their own bet.
        using var strangerClient = host.CreatePlayerHttpClient(other, "Beatriz");
        var (strangerItems, strangerNext) = await MyBetsApi.PageAsync(strangerClient, 50, null, ct);
        Assert.Equal([history.Strangers], strangerItems.Select(i => i.GetProperty("betId").GetGuid()));
        Assert.Equal("lost", Json.Text(strangerItems[0], "status"));
        Assert.Null(strangerNext);
    }

    [Theory(DisplayName = "Dado um limit fora de 1..50, quando GET /api/v1/me/bets, então 400 INVALID_LIMIT no envelope de erro")]
    [Trait("Spec", "SPEC-0025:IT-02")]
    [Trait("Category", "Integration")]
    [InlineData(0)]
    [InlineData(51)]
    public async Task GivenLimitOutOfRangeWhenGettingMyBetsThenInvalidLimit(int limit)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        using var client = host.CreatePlayerHttpClient("player-" + Guid.NewGuid().ToString("N"));

        using var response = await client.GetAsync(MyBetsApi.PageUri(limit), ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.BadRequest, "INVALID_LIMIT", ct);
    }

    [Fact(DisplayName = "Dado um jogador com sessão, quando GET /api/v1/me/bets?limit=abc, então 400 INVALID_LIMIT no envelope de erro")]
    [Trait("Spec", "SPEC-0025:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenNonNumericLimitWhenGettingMyBetsThenInvalidLimitInEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        using var client = host.CreatePlayerHttpClient("player-" + Guid.NewGuid().ToString("N"));

        // Emenda v4: a limit that is not a number is INVALID_LIMIT, in the same envelope as a limit out of range.
        using var response = await client.GetAsync(new Uri(MyBetsApi.Path + "?limit=abc", UriKind.Relative), ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.BadRequest, "INVALID_LIMIT", ct);
    }

    [Fact(DisplayName = "Dado o Host real, quando GET /api/v1/me/bets sem sessão, então 401")]
    [Trait("Spec", "SPEC-0025:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenNoSessionWhenGettingMyBetsThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        using var anonymous = host.CreateHttpClient();

        var (status, body) = await MyBetsApi.GetAsync(anonymous, 10, null, ct);

        Assert.True(status == HttpStatusCode.Unauthorized, $"Expected 401 without a session, got {(int)status}: {body}");
    }

    /// <summary>Round A crashes at 2,00x: the 10,00 is cashed out at ≈ 1 s, the 20,00 is lost; the other player's 5,00 is lost too.</summary>
    private static async Task<History> PlayRoundAAsync(BettingHost host, RoundWatch rounds, BetsClient player, BetsClient stranger, CancellationToken ct)
    {
        var (flight, a) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        [
            BetsClient.BetId(await player.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await player.PlaceAsync(panel: 2, amount: "20.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await stranger.PlaceAsync(panel: 1, amount: "5.00", ct).ConfigureAwait(false)),
        ], ct).ConfigureAwait(false);
        await flight.AtAsync(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        var cashOut = CashOutFlight.CashedOutOf(await player.CashOutAsync(a[0], ct).ConfigureAwait(false));
        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, RoundTimeout).ConfigureAwait(false);
        await Settlements.WaitStatusAsync(host, a[1], "lost", ct).ConfigureAwait(false);
        await Settlements.WaitStatusAsync(host, a[2], "lost", ct).ConfigureAwait(false);
        var roundA = new RoundRef(flight.RoundId, rounds.BettingOf(flight.RoundId).Payload.GetProperty("roundNumber").GetInt64());
        return new History(roundA, new RoundRef(Guid.Empty, 0), a[0], a[1], a[2], Guid.Empty, Guid.Empty, Guid.Empty, Json.Decimal(cashOut, "multiplier"), Json.Decimal(cashOut, "payout"));
    }

    /// <summary>
    /// Round B (armed to be annulled): the 30,00 (auto 1,50x) is cancelled in its betting phase and the 40,00 refunded at its
    /// end; during its flight a 5,00 is queued for the next round — seen in the history without a round — and cancelled.
    /// </summary>
    private static async Task<History> PlayAnnulledRoundBAsync(
        BettingHost host, RoundWatch rounds, BetsClient player, HttpClient client, History history, CancellationToken ct)
    {
        var betting = await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5)).ConfigureAwait(false);
        var cancelled = BetsClient.BetId(await player.PlaceAsync(Guid.NewGuid(), panel: 1, amount: "30.00", autoCashOut: "1.50", ct).ConfigureAwait(false));
        _ = (await player.CancelAsync(cancelled, ct).ConfigureAwait(false)).Value;
        var refunded = BetsClient.BetId(await player.PlaceAsync(panel: 2, amount: "40.00", ct).ConfigureAwait(false));
        var flight = await rounds.NextFlightAsync().ConfigureAwait(false);
        Assert.Equal(betting.RoundId, flight.RoundId);
        await host.WaitActiveAsync([refunded], ct).ConfigureAwait(false);

        var queued = BetsClient.BetId(await player.PlaceAsync(panel: 1, amount: "5.00", ct).ConfigureAwait(false));
        var (whileQueued, _) = await MyBetsApi.PageAsync(client, 50, null, ct).ConfigureAwait(false);
        var queuedItem = Assert.Single(whileQueued, i => i.GetProperty("betId").GetGuid() == queued);
        Assert.Equal("queued", Json.Text(queuedItem, "status"));
        Assert.True(Json.IsNullOrMissing(queuedItem, "roundId"), $"A queued bet without a round has a null roundId: {queuedItem.GetRawText()}");
        Assert.True(Json.IsNullOrMissing(queuedItem, "roundNumber"), $"A queued bet without a round has a null roundNumber: {queuedItem.GetRawText()}");
        _ = (await player.CancelAsync(queued, ct).ConfigureAwait(false)).Value;

        await rounds.Recorder.WaitForAsync("RoundAnnulled", e => e.RoundId == flight.RoundId, RoundTimeout).ConfigureAwait(false);
        await Settlements.WaitStatusAsync(host, refunded, "refunded", ct).ConfigureAwait(false);
        var roundB = new RoundRef(flight.RoundId, betting.Payload.GetProperty("roundNumber").GetInt64());
        return history with { RoundB = roundB, Cancelled = cancelled, Refunded = refunded, Queued = queued };
    }

    /// <summary>placedAt descending, ties by betId descending (SPEC-0025 §6).</summary>
    private static void AssertOrdered(List<JsonElement> items)
    {
        for (var i = 1; i < items.Count; i++)
        {
            var (previous, current) = (items[i - 1], items[i]);
            var (previousAt, currentAt) = (Json.Instant(previous, "placedAt"), Json.Instant(current, "placedAt"));
            Assert.True(
                previousAt > currentAt
                || (previousAt == currentAt && string.CompareOrdinal(Json.Text(previous, "betId"), Json.Text(current, "betId")) > 0),
                $"The history is by placedAt descending, ties by betId descending: {previous.GetRawText()} before {current.GetRawText()}");
        }
    }

    private static void AssertItem(
        JsonElement item,
        string status,
        int panel,
        decimal amount,
        Guid? roundId,
        long? roundNumber,
        decimal? autoCashOut = null,
        decimal? multiplier = null,
        decimal? payout = null)
    {
        var raw = item.GetRawText();
        Assert.True(string.Equals(status, Json.Text(item, "status"), StringComparison.Ordinal), $"Expected status {status}: {raw}");
        Assert.Equal(panel, item.GetProperty("panel").GetInt32());
        Assert.Equal(amount, Json.Decimal(item, "amount"));
        Assert.Equal(roundId, Json.IsNullOrMissing(item, "roundId") ? null : item.GetProperty("roundId").GetGuid());
        Assert.Equal(roundNumber, Json.IsNullOrMissing(item, "roundNumber") ? null : item.GetProperty("roundNumber").GetInt64());
        Assert.Equal(autoCashOut, Json.IsNullOrMissing(item, "autoCashOut") ? null : Json.Decimal(item, "autoCashOut"));
        Assert.Equal(multiplier, Json.IsNullOrMissing(item, "multiplier") ? null : Json.Decimal(item, "multiplier"));
        Assert.Equal(payout, Json.IsNullOrMissing(item, "payout") ? null : Json.Decimal(item, "payout"));
    }

    private sealed record RoundRef(Guid RoundId, long RoundNumber);

    /// <summary>The bets of the scenario: the player's five and the other player's one, and the cash-out of the 10,00.</summary>
    private sealed record History(
        RoundRef RoundA,
        RoundRef RoundB,
        Guid CashedOut,
        Guid Lost,
        Guid Strangers,
        Guid Cancelled,
        Guid Refunded,
        Guid Queued,
        decimal Multiplier,
        decimal Payout);
}
