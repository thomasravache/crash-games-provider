using System.Globalization;
using System.Text;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.DemoCasino.Tests.Support;
using Apogeu.Operators.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Contracts;
using Xunit;
using static Apogeu.DemoCasino.Tests.Support.CasinoWalletApi;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0047 IT-01 (§6): the real DemoCasino (listening on the loopback, real HTTP) and the real Apogeu (the Host in memory
// on real PostgreSQL and Redis, Development seed) with the demo operator in seamless mode, the casino being its wallet.
// The player enters the casino and opens the Pipa (the signed launch of SPEC-0045); the game reads the casino's balance;
// the bet and the cash-out go through the Apogeu's operator wallet port (IOperatorWallet, what PlaceBet and the cash-out
// credit call) to the casino over HTTP, signed with the demo operator key; the casino's balance (GET /casino/balance) and
// the game's reflect the debit and the credit; a bet above the balance is refused (INSUFFICIENT_FUNDS) and changes
// nothing.
[Collection(ApogeuDependencies.Name)]
public sealed class SeamlessWalletJourneyTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dados DemoCasino e Apogeu reais em modo seamless, quando o jogador aposta e retira, então as chamadas chegam assinadas e o saldo do cassino reflete débito e crédito; aposta acima do saldo é recusada")]
    [Trait("Spec", "SPEC-0047:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealCasinoAndApogeuInSeamlessModeWhenPlayerBetsAndCashesOutThenSignedCallsAndCasinoBalanceReflectsThem()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var demo = await SeamlessDemo.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        using var browser = demo.Casino.CreateLoopbackBrowserClient();
        var playerId = await EnterAsync(browser, ct);
        var player = new PlayerRef(new OperatorId(ApogeuApiHost.DemoOperatorId), new PlayerId(playerId));
        var brl = new Currency(CurrencyCode);

        // The casino shows its own wallet: R$ 3.000,00.
        Assert.Equal(OpeningBalance, (await CasinoBalanceAsync(browser, ct)).Balance);

        // The player opens the Pipa; the game reads the balance of the casino wallet, not the Apogeu demo wallet.
        var sessionToken = await OpenPipaAsync(demo, browser, ct);
        Assert.Equal(OpeningBalance, await demo.Apogeu.GameBalanceAsync(sessionToken, ct));

        // Bet of R$ 10,00: debited in the casino.
        var bet = Guid.NewGuid();
        var round = new RoundId(Guid.NewGuid());
        var debit = await demo.Apogeu.WithWalletAsync(wallet => wallet.DebitAsync(
            new WalletOperation(TransactionId.ForDebit(bet), player, brl, new Money(10.00m), round, bet), ct));
        Assert.Equal(2_990.00m, Assert.IsType<WalletOk>(debit).BalanceAfter.Amount.Value);
        Assert.Equal(2_990.00m, (await CasinoBalanceAsync(browser, ct)).Balance);
        Assert.Equal(2_990.00m, await demo.Apogeu.GameBalanceAsync(sessionToken, ct));

        // Cash-out paying R$ 23,40: credited in the casino.
        var credit = await demo.Apogeu.WithWalletAsync(wallet => wallet.CreditAsync(
            new WalletOperation(TransactionId.ForCredit(bet), player, brl, new Money(23.40m), round, bet), ct));
        Assert.Equal(3_013.40m, Assert.IsType<WalletOk>(credit).BalanceAfter.Amount.Value);
        Assert.Equal(3_013.40m, (await CasinoBalanceAsync(browser, ct)).Balance);
        Assert.Equal(3_013.40m, await demo.Apogeu.GameBalanceAsync(sessionToken, ct));

        // A bet above the balance is refused by the casino and changes nothing.
        var tooMuch = Guid.NewGuid();
        var refused = await demo.Apogeu.WithWalletAsync(wallet => wallet.DebitAsync(
            new WalletOperation(TransactionId.ForDebit(tooMuch), player, brl, new Money(5_000.00m), round, tooMuch), ct));
        Assert.Equal(3_013.40m, Assert.IsType<WalletInsufficientFunds>(refused).Balance.Amount.Value);
        Assert.Equal(3_013.40m, (await CasinoBalanceAsync(browser, ct)).Balance);

        // Every call reached the casino signed with the demo operator key (public contract §3) and was answered 200.
        var calls = demo.WalletCalls.Calls;
        Assert.Contains(calls, call => string.Equals(call.Path, DebitPath, StringComparison.Ordinal));
        Assert.Contains(calls, call => string.Equals(call.Path, CreditPath, StringComparison.Ordinal));
        Assert.Contains(calls, call => string.Equals(call.Path, BalancePath, StringComparison.Ordinal));
        Assert.All(calls, call => AssertSignedBy(demo.Key, call));
    }

    /// <summary>The Pipa launched from the casino (POST /casino/play/pipa) and opened as the game does: its player session token.</summary>
    private static async Task<string> OpenPipaAsync(SeamlessDemo demo, HttpClient browser, CancellationToken ct)
    {
        using var play = await CasinoApi.PlayAsync(browser, "pipa", ct).ConfigureAwait(false);
        var launchUrl = await CasinoApi.ReadLaunchUrlAsync(play, ct).ConfigureAwait(false);
        using var opened = await demo.Apogeu.OpenLaunchAsync(launchUrl, ct).ConfigureAwait(false);
        var session = await CasinoApi.ReadOkJsonAsync(opened, ct).ConfigureAwait(false);
        return session.GetProperty("sessionToken").GetString()!;
    }

    private static void AssertSignedBy(TestApiKey key, ArrivedWalletCall call)
    {
        Assert.Equal(key.Id, call.Header(KeyHeader));
        var timestamp = call.Header(TimestampHeader);
        Assert.True(long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unix), $"{call.Path}: X-Apogeu-Timestamp must be Unix seconds, got '{timestamp}'.");
        Assert.True(Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unix) <= 300, $"{call.Path}: timestamp out of the ±300 s window.");
        Assert.Equal(ReferenceSignature.Compute(key.SecretBytes, unix, "POST", call.Path, call.Body), call.Header(SignatureHeader));
        Assert.True(call.Status == 200, $"{call.Path}: the casino answered {call.Status} to a signed call: {Encoding.UTF8.GetString(call.Body)}");
    }
}
