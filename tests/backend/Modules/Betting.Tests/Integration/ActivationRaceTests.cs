using Apogeu.Betting.Contracts;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class ActivationRaceTests(BettingContainers containers)
{
    private static readonly TimeSpan ActivationTimeout = TimeSpan.FromSeconds(30);

    [Fact(DisplayName = "Dada uma aposta queued no painel 1 e uma nova aposta no mesmo painel na corrida com a abertura da rodada seguinte, quando a rodada abre, então a nova recebe PANEL_BUSY, a queued vira placed com BetActivated e as queued dos outros jogadores também são ativadas")]
    [Trait("Spec", "SPEC-0022:IT-07")]
    [Trait("Category", "Integration")]
    public async Task GivenQueuedBetAndRacingBetOnSamePanelWhenRoundOpensThenPanelBusyAndAllQueuedBetsActivated()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct, services: services =>
            services.Decorate<IRoundFeed>((inner, _) => new BlockingRoundFeed(inner)));
        var feed = Assert.IsType<BlockingRoundFeed>(host.Services.GetRequiredService<IRoundFeed>());
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var alice = await BetsClient.ConnectAsync(host, "player-a-" + Guid.NewGuid().ToString("N"), ct);
        await using var bob = await BetsClient.ConnectAsync(host, "player-b-" + Guid.NewGuid().ToString("N"), ct);

        Guid aliceQueuedId;
        Guid bobQueuedId;
        try
        {
            // Queued bets of two players of the room, in panel 1.
            await rounds.NextFlightAsync();
            var aliceQueued = (await alice.PlaceAsync(panel: 1, amount: "10.00", ct)).Value;
            var bobQueued = (await bob.PlaceAsync(panel: 1, amount: "15.00", ct)).Value;
            Assert.Equal("queued", Json.Text(aliceQueued, "status"));
            Assert.Equal("queued", Json.Text(bobQueued, "status"));
            aliceQueuedId = aliceQueued.GetProperty("betId").GetGuid();
            bobQueuedId = bobQueued.GetProperty("betId").GetGuid();

            // The next round opens and is held before Betting activates its queued bets: Alice races it on panel 1.
            feed.HoldNextOpening();
            await feed.Held.WaitAsync(TimeSpan.FromSeconds(60), ct);
            var racing = await alice.PlaceAsync(panel: 1, amount: "20.00", ct);
            Assert.True(racing.FailedWith(BettingErrorCodes.PanelBusy), $"The queued bet holds panel 1 until it is activated or cancelled; got {racing}");
        }
        finally
        {
            feed.Release();
        }

        var aliceActivated = await alice.WaitForActivationAsync(aliceQueuedId, ActivationTimeout);
        var bobActivated = await bob.WaitForActivationAsync(bobQueuedId, ActivationTimeout);
        Assert.Equal(aliceActivated.GetProperty("roundId").GetGuid(), bobActivated.GetProperty("roundId").GetGuid());
        Assert.Equal(2, ((FakeOperatorWallet)host.Wallet).AppliedDebits.Count);
    }
}
