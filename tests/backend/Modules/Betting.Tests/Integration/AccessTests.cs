using System.Net;
using Apogeu.Betting.Contracts;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class AccessTests(BettingContainers containers)
{
    [Fact(DisplayName = "Dadas sessões de dois jogadores, quando um cancela a aposta do outro (ou um betId inexistente), então BET_NOT_FOUND e a aposta do outro continua intacta")]
    [Trait("Spec", "SPEC-0022:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenTwoPlayersWhenOneCancelsTheOthersBetThenBetNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        var wallet = (FakeOperatorWallet)host.Wallet;
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var alice = await BetsClient.ConnectAsync(host, "player-a-" + Guid.NewGuid().ToString("N"), ct);
        await using var bob = await BetsClient.ConnectAsync(host, "player-b-" + Guid.NewGuid().ToString("N"), ct);

        await rounds.NextBettingAsync(TimeSpan.FromSeconds(2));
        var aliceBet = BetsClient.BetId(await alice.PlaceAsync(panel: 1, amount: "10.00", ct));

        var stolen = await bob.CancelAsync(aliceBet, ct);
        var unknown = await bob.CancelAsync(Guid.NewGuid(), ct);

        Assert.True(stolen.FailedWith(BettingErrorCodes.BetNotFound), $"Expected BET_NOT_FOUND, got {stolen}");
        Assert.True(unknown.FailedWith(BettingErrorCodes.BetNotFound), $"Expected BET_NOT_FOUND, got {unknown}");
        Assert.Empty(wallet.AppliedRollbacks);
        Assert.Equal(2990.00m, wallet.BalanceOf(BettingHost.PlayerRef(alice.Player), BettingHost.Brl));

        // The bet is still Alice's to cancel.
        var cancelled = (await alice.CancelAsync(aliceBet, ct)).Value;
        Assert.Equal(10.00m, Json.Decimal(cancelled, "refunded"));
    }

    [Fact(DisplayName = "Dado o hub /hubs/bets, quando uma conexão sem token tenta conectar para apostar, então 401")]
    [Trait("Spec", "SPEC-0022:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenNoTokenWhenConnectingToBetsHubThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);

        using var http = host.CreateHttpClient();
        using var negotiate = await http.PostAsync(new Uri("/hubs/bets/negotiate?negotiateVersion=1", UriKind.Relative), content: null, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, negotiate.StatusCode);

        await using var anonymous = host.CreateAnonymousBetsConnection();
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => anonymous.StartAsync(ct));
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        // Control: the hub exists and accepts a valid session.
        await using var player = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct);
        Assert.Equal(Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected, player.Connection.State);
    }

    [Fact(DisplayName = "Dada uma sessão em modo dinheiro real (o modo vem do token), quando o jogador aposta, então REAL_MONEY_DISABLED sem débito")]
    [Trait("Spec", "SPEC-0022:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenRealMoneySessionWhenPlayerBetsThenRealMoneyDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct);
        var wallet = (FakeOperatorWallet)host.Wallet;
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var real = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct, mode: "real");

        await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
        var refused = await real.PlaceAsync(panel: 1, amount: "10.00", ct);

        Assert.True(refused.FailedWith(BettingErrorCodes.RealMoneyDisabled), $"Expected REAL_MONEY_DISABLED, got {refused}");
        Assert.DoesNotContain(wallet.Calls, c => string.Equals(c.Kind, "debit", StringComparison.Ordinal));
    }
}
