using System.Globalization;
using System.Text.Json;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.PlaceBet;
using Apogeu.Betting.Features.Sessions;
using Apogeu.Betting.Tests.Support;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// IT-03 (SPEC-0025 §5, ADR-0019): with 1.000 bets in one round, the room's group receives at most 5 <c>LiveBetsBatch</c> per
/// second, each with at most 100 items. The bets go through the module's own <c>PlaceBet</c> command (the use case the hub
/// only translates to) from 500 players in parallel, in a betting phase widened by the room's configuration; the batches
/// are observed by a real SignalR client that joined the room with <c>JoinSalaBets</c>.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class LiveBetsBandwidthTests(BettingContainers containers)
{
    private const int Players = 500;
    private const int BetCount = Players * 2;
    private const int Parallelism = 25;
    private static readonly TimeSpan Observation = TimeSpan.FromSeconds(5);

    [Fact(DisplayName = "Dadas 1.000 apostas numa rodada, quando a difusão é observada por 5 s, então cada segundo inteiro desde o primeiro lote tem no máximo 5 lotes e cada lote ≤ 100 itens")]
    [Trait("Spec", "SPEC-0025:IT-03")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenThousandBetsInOneRoundWhenBroadcastIsObservedThenAtMostFiveBatchesPerSecondOfAtMostHundredItems()
    {
        var ct = TestContext.Current.CancellationToken;

        // 30 s of bets so the 1.000 bets land in one betting phase.
        await using var host = await BettingHost.StartAsync(containers, ct, sala: new BettingSala(BettingDuration: "00:00:30"));
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var observer = await BetsClient.ConnectAsync(host, "observer-" + Guid.NewGuid().ToString("N"), ct, nickname: "Observador");
        var joined = await observer.JoinSalaBetsAsync(ct);
        Assert.True(joined.Error is null, $"JoinSalaBets of the observer: {joined}");

        var betting = await rounds.NextBettingAsync(TimeSpan.FromSeconds(20));
        await PlaceThousandBetsAsync(host, ct);
        Assert.True(DateTimeOffset.UtcNow < Json.Instant(betting.Payload, "bettingEndsAt"), "Precondition: the 1.000 bets were placed in one betting phase.");

        // Observes for 5 s from the first batch, and until a batch carries the 1.000 bets in its totals.
        var firstBatch = await observer.WaitForLiveBatchAsync(_ => true, TimeSpan.FromSeconds(10));
        await observer.WaitForLiveBatchAsync(b => b.GetProperty("totalBets").GetInt32() == BetCount, TimeSpan.FromSeconds(20));
        var remaining = firstBatch.ReceivedAt + Observation - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, ct);
        }

        var batches = observer.LiveBatches.Where(b => b.RoundId == betting.RoundId).ToList();
        var perSecond = batches
            .GroupBy(b => (int)Math.Floor((b.ReceivedAt - firstBatch.ReceivedAt).TotalSeconds))
            .OrderBy(g => g.Key)
            .Select(g => (Second: g.Key, Count: g.Count()))
            .ToList();
        var describe = string.Join(", ", perSecond.Select(s => string.Create(CultureInfo.InvariantCulture, $"s{s.Second}={s.Count}")));
        Assert.True(perSecond.All(s => s.Count <= 5), $"At most 5 batches per whole second from the first batch; got {describe}.");
        Assert.All(batches, b =>
        {
            Assert.True(Count(b.Payload, "placed") <= 100, $"At most 100 placed per batch, got {Count(b.Payload, "placed")}.");
            Assert.True(Count(b.Payload, "cashedOut") <= 100, $"At most 100 cash-outs per batch, got {Count(b.Payload, "cashedOut")}.");
        });

        // The totals account for every bet even when a batch leaves some out.
        var last = batches[^1].Payload;
        Assert.Equal(BetCount, last.GetProperty("totalBets").GetInt32());
        Assert.Equal(Players, last.GetProperty("totalPlayers").GetInt32());
        Assert.Equal(BetCount * 1.00m, Json.Decimal(last, "totalWagered"));
        Assert.True(batches.Sum(b => Count(b.Payload, "placed")) <= BetCount, "No bet is broadcast twice.");
    }

    /// <summary>500 players bet 1,00 in both panels through <see cref="PlaceBetUseCase"/>, <see cref="Parallelism"/> at a time.</summary>
    private static async Task PlaceThousandBetsAsync(BettingHost host, CancellationToken ct)
    {
        var requests = Enumerable.Range(0, Players).SelectMany(player => (int[])[1, 2], (player, panel) => (Player: player, Panel: panel)).ToList();
        using var gate = new SemaphoreSlim(Parallelism);
        var placements = requests.Select(async request =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var scope = host.Services.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    var placeBet = scope.ServiceProvider.GetRequiredService<PlaceBetUseCase>();
                    var name = "volume-" + request.Player.ToString(CultureInfo.InvariantCulture);
                    var session = new BettingSession(
                        new BetOwner(new OperatorId(BettingSessions.Operator), new PlayerId(name), new Currency(BettingSessions.Currency)),
                        BettingSala.SalaId,
                        BettingSession.DemoMode,
                        "Jogador" + request.Player.ToString(CultureInfo.InvariantCulture));
                    var outcome = await placeBet.PlaceAsync(session, new PlaceBetRequest(Guid.NewGuid(), request.Panel, new Money(1.00m), null), ct).ConfigureAwait(false);
                    Assert.True(outcome.Value is { Status: BetAcceptedStatus.Placed }, $"Bet {request} was not placed: {outcome.Code}");
                }
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(placements).ConfigureAwait(false);
    }

    private static int Count(JsonElement payload, string property) =>
        payload.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array ? array.GetArrayLength() : 0;
}
