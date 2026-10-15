using System.Text.Json;
using System.Text.RegularExpressions;
using Apogeu.DemoCasino.Tests.Support;
using Apogeu.Wallet.Contracts;
using Xunit;
using static Apogeu.DemoCasino.Tests.Support.CasinoWalletApi;

namespace Apogeu.DemoCasino.Tests.Contract;

// SPEC-0047 CT-01 (§6): the real answers of the DemoCasino wallet API, for every kind of call the Apogeu makes (balance,
// debit with and without round, duplicate, insufficient funds, credit, rollback, repeated rollback, unknown rollback),
// checked against the public contract (SPEC-0021@4, docs/integration/operator-wallet-api.md §5): 200 application/json; a
// `status` of the contract table (the values the Apogeu reads, Wallet.Contracts OperatorWalletStatus); `balance` a decimal
// string with two places; `currency` and `transactionId` repeating the request's; the documented balance semantics.
public sealed partial class CasinoWalletContractTests
{
    private static readonly string[] ContractStatuses =
    [
        OperatorWalletStatus.Ok,
        OperatorWalletStatus.InsufficientFunds,
        OperatorWalletStatus.Duplicate,
        OperatorWalletStatus.PlayerNotFound,
        OperatorWalletStatus.TransactionNotFound,
    ];

    [Fact(DisplayName = "Dadas as respostas reais do DemoCasino, quando validadas contra o contrato público da carteira, então conferem")]
    [Trait("Spec", "SPEC-0047:CT-01")]
    [Trait("Category", "Contract")]
    public async Task GivenRealCasinoAnswersWhenValidatedAgainstPublicWalletContractThenTheyMatch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var casino = DemoCasinoHost.Start();
        using var browser = casino.CreateBrowserClient();
        var playerId = await EnterAsync(browser, ct);
        using var apogeu = casino.CreateRawClient();
        async Task<WalletAnswer> Checked(WalletCall call, string status, decimal balance)
        {
            var answer = await CallAsync(apogeu, call, casino.OperatorKeyId, casino.OperatorSecret, ct).ConfigureAwait(false);
            AssertContractShape(call, answer);
            Assert.True(string.Equals(status, answer.Status, StringComparison.Ordinal), $"POST {call.Path}: expected status {status}: {answer.Json.GetRawText()}");
            Assert.Equal(balance, Amount(answer.Balance));
            return answer;
        }

        var bet = Guid.NewGuid();
        var queued = Guid.NewGuid();

        await Checked(Balance(playerId), OperatorWalletStatus.Ok, 3_000.00m);
        await Checked(Debit(playerId, 10.00m, bet), OperatorWalletStatus.Ok, 2_990.00m);

        // DUPLICATE: the balance right after the original operation (§5).
        await Checked(Debit(playerId, 10.00m, bet), OperatorWalletStatus.Duplicate, 2_990.00m);

        // A debit of a bet queued for the next round carries roundId null (§4).
        await Checked(Debit(playerId, 5.00m, queued, queued: true), OperatorWalletStatus.Ok, 2_985.00m);

        // INSUFFICIENT_FUNDS: nothing debited, balance is the current one.
        await Checked(Debit(playerId, 5_000.00m, Guid.NewGuid()), OperatorWalletStatus.InsufficientFunds, 2_985.00m);

        await Checked(Credit(playerId, 20.00m, bet), OperatorWalletStatus.Ok, 3_005.00m);

        // Rollback gives back exactly the original debit; repeated, DUPLICATE and never twice.
        await Checked(Rollback(playerId, queued), OperatorWalletStatus.Ok, 3_010.00m);
        await Checked(Rollback(playerId, queued), OperatorWalletStatus.Duplicate, 3_010.00m);

        // TRANSACTION_NOT_FOUND: only on rollback; nothing posted.
        await Checked(Rollback(playerId, Guid.NewGuid()), OperatorWalletStatus.TransactionNotFound, 3_010.00m);
        await Checked(Balance(playerId), OperatorWalletStatus.Ok, 3_010.00m);
    }

    [Fact(DisplayName = "Dado GET /casino/balance do jogador logado, quando lido, então { balance, currency } com o saldo da carteira do cassino")]
    [Trait("Spec", "SPEC-0047:CT-01")]
    [Trait("Category", "Contract")]
    public async Task GivenCasinoBalanceWhenReadThenBalanceAndCurrencyOfCasinoWallet()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var casino = DemoCasinoHost.Start();
        using var browser = casino.CreateBrowserClient();
        _ = await EnterAsync(browser, ct);

        using var response = await browser.GetAsync(new Uri(CasinoBalancePath, UriKind.Relative), ct);
        var body = await CasinoApi.ReadOkJsonAsync(response, ct);

        Assert.Equal(["balance", "currency"], body.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(CurrencyCode, body.GetProperty("currency").GetString());
        var (balance, _) = await CasinoBalanceAsync(browser, ct);
        Assert.Equal(OpeningBalance, balance);

        // Without a casino session there is no balance to show.
        using var anonymous = casino.CreateBrowserClient();
        using var refused = await anonymous.GetAsync(new Uri(CasinoBalancePath, UriKind.Relative), ct);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    private static void AssertContractShape(WalletCall call, WalletAnswer answer)
    {
        var raw = answer.Json.GetRawText();
        Assert.True(answer.Status is not null && ContractStatuses.Contains(answer.Status, StringComparer.Ordinal), $"POST {call.Path}: status outside the contract: {raw}");
        Assert.True(answer.Json.GetProperty("balance").ValueKind == JsonValueKind.String, $"POST {call.Path}: balance must be a string, never a JSON number: {raw}");
        Assert.True(answer.Balance is not null && TwoPlaces().IsMatch(answer.Balance), $"POST {call.Path}: balance must be a decimal string with two places: {raw}");
        Assert.True(string.Equals(CurrencyCode, answer.Currency, StringComparison.Ordinal), $"POST {call.Path}: currency must repeat the request's: {raw}");
        if (call.TransactionId is not null)
        {
            Assert.True(string.Equals(call.TransactionId, answer.TransactionId, StringComparison.Ordinal), $"POST {call.Path}: transactionId must repeat the request's: {raw}");
        }
    }

    [GeneratedRegex(@"^-?\d+\.\d{2}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TwoPlaces();
}
