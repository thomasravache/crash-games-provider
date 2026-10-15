using Apogeu.DemoCasino.Tests.Support;
using Xunit;
using static Apogeu.DemoCasino.Tests.Support.CasinoWalletApi;

namespace Apogeu.DemoCasino.Tests.Unit;

// SPEC-0047 UT-01 (§6): the rules of the casino wallet, through the public wallet API the DemoCasino implements
// (SPEC-0021@4, docs/integration/operator-wallet-api.md) on the in-memory DemoCasino, with no external dependency: the
// opening balance is R$ 3.000,00 per player; a debit applies once (OK), the same transactionId again is DUPLICATE with the
// original result (the balance right after the original debit, even after later operations); a credit is OK; a rollback
// of a debit that never happened is TRANSACTION_NOT_FOUND and changes nothing. Driven through the HTTP API (as
// LaunchBodyTests of SPEC-0045), so the test holds whatever internal type the implementation uses.
public sealed class CasinoWalletRulesTests
{
    [Fact(DisplayName = "Dada a carteira do cassino, quando débito, débito repetido, crédito e estorno sem débito, então OK, DUPLICATE com o mesmo resultado, OK e TRANSACTION_NOT_FOUND")]
    [Trait("Spec", "SPEC-0047:UT-01")]
    public async Task GivenCasinoWalletWhenDebitRepeatCreditAndUnknownRollbackThenOkDuplicateOkAndTransactionNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var casino = DemoCasinoHost.Start();
        using var browser = casino.CreateBrowserClient();
        var playerId = await EnterAsync(browser, ct);
        using var apogeu = casino.CreateRawClient();
        Task<WalletAnswer> Call(WalletCall call) => CallAsync(apogeu, call, casino.OperatorKeyId, casino.OperatorSecret, ct);

        // Opening balance: R$ 3.000,00 per player.
        var opening = await Call(Balance(playerId));
        Assert.Equal("OK", opening.Status);
        Assert.Equal(OpeningBalance, Amount(opening.Balance));

        // Debit: applied once.
        var bet = Guid.NewGuid();
        var debit = Debit(playerId, 10.00m, bet);
        var debited = await Call(debit);
        Assert.Equal("OK", debited.Status);
        Assert.Equal(2_990.00m, Amount(debited.Balance));
        Assert.Equal(DebitId(bet), debited.TransactionId);

        // The same transactionId again: DUPLICATE with the original result, nothing debited twice.
        var repeated = await Call(debit);
        Assert.Equal("DUPLICATE", repeated.Status);
        Assert.Equal(2_990.00m, Amount(repeated.Balance));
        Assert.Equal(DebitId(bet), repeated.TransactionId);
        Assert.Equal(2_990.00m, Amount((await Call(Balance(playerId))).Balance));

        // Credit: OK with the balance right after it.
        var credited = await Call(Credit(playerId, 25.50m, bet));
        Assert.Equal("OK", credited.Status);
        Assert.Equal(3_015.50m, Amount(credited.Balance));
        Assert.Equal(CreditId(bet), credited.TransactionId);

        // A repetition after later operations still answers the original result (the balance right after the debit).
        var late = await Call(debit);
        Assert.Equal("DUPLICATE", late.Status);
        Assert.Equal(2_990.00m, Amount(late.Balance));

        // Rollback of a debit that never happened: TRANSACTION_NOT_FOUND, nothing posted.
        var unknown = Guid.NewGuid();
        var notFound = await Call(Rollback(playerId, unknown));
        Assert.Equal("TRANSACTION_NOT_FOUND", notFound.Status);
        Assert.Equal(RollbackId(unknown), notFound.TransactionId);
        Assert.Equal(3_015.50m, Amount((await Call(Balance(playerId))).Balance));

        // The casino shows the same balance to its player (GET /casino/balance, §6).
        var shown = await CasinoBalanceAsync(browser, ct);
        Assert.Equal(3_015.50m, shown.Balance);
        Assert.Equal(CurrencyCode, shown.Currency);
    }

    [Fact(DisplayName = "Dada a carteira do cassino, quando o débito passa do saldo, então INSUFFICIENT_FUNDS com o saldo atual e nada debitado")]
    [Trait("Spec", "SPEC-0047:UT-01")]
    public async Task GivenCasinoWalletWhenDebitAboveBalanceThenInsufficientFundsAndNothingDebited()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var casino = DemoCasinoHost.Start();
        using var browser = casino.CreateBrowserClient();
        var playerId = await EnterAsync(browser, ct);
        using var apogeu = casino.CreateRawClient();

        var refused = await CallAsync(apogeu, Debit(playerId, 3_000.01m, Guid.NewGuid()), casino.OperatorKeyId, casino.OperatorSecret, ct);

        Assert.Equal("INSUFFICIENT_FUNDS", refused.Status);
        Assert.Equal(OpeningBalance, Amount(refused.Balance));
        Assert.Equal(OpeningBalance, Amount((await CallAsync(apogeu, Balance(playerId), casino.OperatorKeyId, casino.OperatorSecret, ct)).Balance));
    }
}
