using Apogeu.Betting.Contracts;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// UT-06 (emenda 3, second review): a pending reservation is a state of the bet store, not of the domain bet, so this case
/// of UT-06 runs on the real Host and schema instead of in memory.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class PendingPanelTests(BettingContainers containers)
{
    [Fact(DisplayName = "Dada uma reserva pendente no painel 1 (débito em andamento), quando chega outra aposta do mesmo jogador no painel 1, então PANEL_BUSY e a pendente é aceita")]
    [Trait("Spec", "SPEC-0022:UT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenPendingReservationOnPanelOneWhenAnotherBetOnPanelOneArrivesThenPanelBusy()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        await using var host = await BettingHost.StartAsync(containers, ct, wallet);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(player);
        await using var first = await BetsClient.ConnectAsync(host, player, ct);
        await using var second = await BetsClient.ConnectAsync(host, player, ct);
        var debiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The debit of the 10,00 bet stays in progress until the second bet was answered.
        wallet.BeforeDebit = async op =>
        {
            if (op.Player == account && op.Amount.Value == 10.00m)
            {
                debiting.TrySetResult();
                await release.Task.ConfigureAwait(false);
            }
        };

        HubOutcome racing;
        Task<HubOutcome> pending;
        try
        {
            await rounds.NextBettingAsync(TimeSpan.FromSeconds(2));
            pending = first.PlaceAsync(panel: 1, amount: "10.00", ct);
            await debiting.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            racing = await second.PlaceAsync(panel: 1, amount: "20.00", ct);
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.True(racing.FailedWith(BettingErrorCodes.PanelBusy), $"The pending reservation holds panel 1; got {racing}");
        var accepted = (await pending.WaitAsync(TimeSpan.FromSeconds(30), ct)).Value;
        Assert.Equal("placed", Json.Text(accepted, "status"));
        Assert.Single(wallet.Inner.AppliedDebits);
    }
}
