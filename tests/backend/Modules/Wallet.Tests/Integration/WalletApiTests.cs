using System.Net;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Tests.Support;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>
/// IT-04: the player REST API of the wallet on the real Host (SPEC-0021 §5, §6): each session only reads and recharges its
/// own balance, the recharge works below the minimum bet of SPEC-0020 (1,00 BRL) and is refused with 409 at or above it,
/// and without a session both endpoints answer 401.
/// </summary>
[Collection(WalletApiDependencies.Name)]
public sealed class WalletApiTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dado o Host real com sessões de dois jogadores, quando cada um lê o saldo e pede recarga, então cada um só vê o próprio saldo, a recarga funciona abaixo do mínimo e retorna 409 acima")]
    [Trait("Spec", "SPEC-0021:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenTwoPlayerSessionsWhenReadingAndRechargingThenEachSeesOnlyOwnBalance()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await WalletApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        var playerA = NewPlayer();
        var playerB = NewPlayer();

        // Player A bets down to 0,50, below the minimum bet; player B keeps the opening balance.
        AssertOk(await host.WithWalletAsync(wallet => wallet.DebitAsync(Debit(playerA, 2_999.50m, Guid.NewGuid()), ct)), 0.50m);
        using var clientA = host.CreateClient(playerA.PlayerId.Value);
        using var clientB = host.CreateClient(playerB.PlayerId.Value);

        var (statusA, bodyA, _) = await WalletApi.SendAsync(clientA, WalletApi.GetBalance(), ct);
        WalletApi.AssertBalanceBody(statusA, bodyA, 0.50m, mode: "demo");
        var (statusB, bodyB, _) = await WalletApi.SendAsync(clientB, WalletApi.GetBalance(), ct);
        WalletApi.AssertBalanceBody(statusB, bodyB, OpeningBalance, mode: "demo");

        var (rechargeA, rechargeBodyA, _) = await WalletApi.SendAsync(clientA, WalletApi.PostRecharge(), ct);
        WalletApi.AssertBalanceBody(rechargeA, rechargeBodyA, OpeningBalance, mode: null);

        var (rechargeB, rechargeBodyB, mediaTypeB) = await WalletApi.SendAsync(clientB, WalletApi.PostRecharge(), ct);
        Assert.True(rechargeB == HttpStatusCode.Conflict, $"Expected 409 RECHARGE_NOT_ALLOWED, got {(int)rechargeB}: {rechargeBodyB}");
        Assert.Equal("application/problem+json", mediaTypeB);
        Assert.Equal("RECHARGE_NOT_ALLOWED", ErrorBodies.CodeOf(rechargeBodyB));

        // After A's recharge and B's refusal: A has 3.000,00 again and B was never touched.
        var (afterA, afterBodyA, _) = await WalletApi.SendAsync(clientA, WalletApi.GetBalance(), ct);
        WalletApi.AssertBalanceBody(afterA, afterBodyA, OpeningBalance, mode: "demo");
        Assert.Equal(BrlBalance(OpeningBalance), await host.WithWalletAsync(wallet => wallet.GetBalanceAsync(playerA, Brl, ct)));
        Assert.Equal(BrlBalance(OpeningBalance), await host.WithWalletAsync(wallet => wallet.GetBalanceAsync(playerB, Brl, ct)));
    }

    [Fact(DisplayName = "Dado o Host real, quando o saldo ou a recarga são pedidos sem sessão, então 401")]
    [Trait("Spec", "SPEC-0021:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenNoSessionWhenReadingOrRechargingThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await WalletApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        using var anonymous = host.CreateAnonymousClient();

        var (balance, balanceBody, _) = await WalletApi.SendAsync(anonymous, WalletApi.GetBalance(), ct);
        var (recharge, rechargeBody, _) = await WalletApi.SendAsync(anonymous, WalletApi.PostRecharge(), ct);

        Assert.True(balance == HttpStatusCode.Unauthorized, $"Expected 401 from GET {WalletApi.BalancePath}, got {(int)balance}: {balanceBody}");
        Assert.True(recharge == HttpStatusCode.Unauthorized, $"Expected 401 from POST {WalletApi.RechargePath}, got {(int)recharge}: {rechargeBody}");
    }
}
