using System.Diagnostics;
using System.Globalization;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.Betting.Tests.Support;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class SettlementVolumeTests(BettingContainers containers)
{
    private const int Players = 2_500;
    private const int BetCount = Players * 2;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    [Fact(DisplayName = "Dadas 5.000 apostas active numa rodada, quando o crash é liquidado, então termina em ≤ 2 s")]
    [Trait("Spec", "SPEC-0024:IT-04")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenFiveThousandActiveBetsWhenCrashIsSettledThenWithinTwoSeconds()
    {
        var ct = TestContext.Current.CancellationToken;

        // A minute of bets to write the 5.000 bets, then a flight of ≈ 3,5 s crashing at 2,00x.
        var sala = new BettingSala(BettingDuration: "00:01:00");
        await using var host = await BettingHost.StartAsync(containers, ct, new ProgrammableWallet(), services => FixedCrashFairness.Register(services, 2.00m), sala);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var betting = await rounds.NextBettingAsync(TimeSpan.FromSeconds(50));
        var window = new RoundWindow(
            new RoundId(betting.RoundId),
            betting.Payload.GetProperty("roundNumber").GetInt64(),
            Json.Instant(betting.Payload, "bettingEndsAt"),
            RoundPhase.Betting);

        // The bets are written through the module's own store (reserved, then confirmed), as PlaceBet does after the debit.
        var bets = await WriteBetsAsync(host, window, ct);
        Assert.True(DateTimeOffset.UtcNow < window.BettingEndsAt, "Precondition: the 5.000 bets were written during the betting phase.");
        await rounds.NextFlightAsync();
        await host.WaitActiveAsync(bets, ct);

        var crashed = await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == betting.RoundId, TimeSpan.FromSeconds(60));
        var lost = 0;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(60))
        {
            lost = await host.CountStatusAsync(bets, "lost", ct);
            if (lost == BetCount && (await host.SettlementsAsync(betting.RoundId, ct)).Count == 1)
            {
                break;
            }

            await Task.Delay(50, ct);
        }

        var settledIn = DateTimeOffset.UtcNow - crashed.ReceivedAt;
        Assert.True(lost == BetCount, $"{lost} of {BetCount} bets lost {settledIn.TotalSeconds:0.0} s after the crash.");
        Assert.True(
            settledIn <= Budget,
            string.Create(CultureInfo.InvariantCulture, $"The crash of {BetCount} bets was settled in {settledIn.TotalMilliseconds:0} ms (budget {Budget.TotalMilliseconds} ms)."));
        var summary = Assert.Single(await host.SettlementsAsync(betting.RoundId, ct));
        Assert.Equal(BetCount, summary.BetsCount);
        Assert.Equal(BetCount * 1.00m, summary.TotalWagered.Value);
        Assert.Equal(BetCount * 1.00m, summary.Ggr.Value);
    }

    /// <summary>2.500 players bet 1,00 in both panels of <paramref name="window"/>, 10 writes at a time.</summary>
    private static async Task<IReadOnlyList<Guid>> WriteBetsAsync(BettingHost host, RoundWindow window, CancellationToken ct)
    {
        var rules = new BetRules(MinBet: 1.00m, MaxBet: 1000.00m, MaxAutoCashOut: 2.00m);
        var requests = Enumerable.Range(0, Players)
            .SelectMany(player => (int[])[1, 2], (player, panel) => (Owner: Owner(player), Panel: panel))
            .ToList();
        var ids = new List<Guid>(BetCount);
        foreach (var batch in requests.Chunk(10))
        {
            ids.AddRange(await Task.WhenAll(batch.Select(async request =>
            {
                var scope = host.Services.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    var store = scope.ServiceProvider.GetRequiredService<IBetStore>();
                    var now = DateTimeOffset.UtcNow;
                    var placement = Bet.Place(Guid.NewGuid(), request.Owner, new BetRequest(Guid.NewGuid(), request.Panel, 1.00m, null), rules, window, now, []);
                    var bet = Assert.IsType<BetPlacementAccepted>(placement).Bet;
                    await store.ReserveAsync(bet, BettingSala.SalaId, now, ct).ConfigureAwait(false);
                    Assert.Equal(BetConfirmation.Confirmed, await store.ConfirmAsync(bet.Id, FakeOperatorWallet.OpeningBalance - 1.00m, ct).ConfigureAwait(false));
                    return bet.Id;
                }
            })).ConfigureAwait(false));
        }

        return ids;
    }

    private static BetOwner Owner(int player) =>
        new(new OperatorId(BettingSessions.Operator), new PlayerId("volume-" + player.ToString(CultureInfo.InvariantCulture)), new Currency(BettingSessions.Currency));
}
