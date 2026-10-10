using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Infrastructure.Demo;
using Apogeu.Wallet.Tests.Support;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Unit;

/// <summary>
/// UT-01..UT-05: the demo adapter of <see cref="IOperatorWallet"/> and <see cref="IDemoWallet"/> (SPEC-0021 §6) over an
/// in-memory ledger: implicit opening balance, debit, insufficient funds, idempotency, credit, rollback and recharge.
/// </summary>
public sealed class DemoWalletTests
{
    private readonly InMemoryWalletLedger _ledger = new();
    private readonly DemoWallet _wallet;

    public DemoWalletTests() => _wallet = new DemoWallet(_ledger, new FixedOperatorLimits(), TimeProvider.System);

    [Fact(DisplayName = "Dado um jogador sem lançamentos, quando o saldo demo é lido, então é 3.000,00 BRL (sem lançamento de abertura)")]
    [Trait("Spec", "SPEC-0021:UT-01")]
    public async Task GivenPlayerWithoutEntriesWhenDemoBalanceIsReadThenItIsThreeThousandBrl()
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();

        var balance = await _wallet.GetBalanceAsync(player, Brl, ct);

        Assert.Equal(BrlBalance(OpeningBalance), balance);
        Assert.Empty(_ledger.Entries);
    }

    [Fact(DisplayName = "Dado saldo 3.000,00, quando débito de 10,00 e depois débito de 5.000,00, então Ok com 2.990,00 e InsufficientFunds com saldo inalterado; cada lançamento soma zero")]
    [Trait("Spec", "SPEC-0021:UT-02")]
    public async Task GivenOpeningBalanceWhenDebitingThenOkThenInsufficientFundsAndEntriesSumZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();
        var bet = Guid.NewGuid();

        var debited = await _wallet.DebitAsync(Debit(player, 10.00m, bet), ct);
        var refused = await _wallet.DebitAsync(Debit(player, 5_000.00m, Guid.NewGuid()), ct);

        AssertOk(debited, 2_990.00m);
        var insufficient = Assert.IsType<WalletInsufficientFunds>(refused);
        Assert.Equal(BrlBalance(2_990.00m), insufficient.Balance);
        Assert.Equal(BrlBalance(2_990.00m), await _wallet.GetBalanceAsync(player, Brl, ct));

        var entry = Assert.Single(_ledger.Entries);
        Assert.Equal(DebitId(bet), entry.TransactionId);
        AssertBalancedEntry(entry, player, -10.00m);
    }

    [Fact(DisplayName = "Dado um débito aplicado, quando o mesmo TransactionId chega de novo, então Duplicate com o resultado original e nenhum lançamento novo")]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task GivenAppliedDebitWhenSameTransactionIdArrivesAgainThenDuplicateWithOriginalResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();
        var debit = Debit(player, 10.00m, Guid.NewGuid());

        var first = await _wallet.DebitAsync(debit, ct);
        var again = await _wallet.DebitAsync(debit, ct);

        AssertOk(first, 2_990.00m);
        AssertDuplicateOfOk(again, 2_990.00m);
        Assert.Single(_ledger.Entries);
        Assert.Equal(BrlBalance(2_990.00m), await _wallet.GetBalanceAsync(player, Brl, ct));
    }

    [Fact(DisplayName = "Dado um betId, quando os TransactionId da aposta são gerados, então são bet:{betId}:debit|credit|rollback (determinísticos)")]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public void GivenBetIdWhenTransactionIdsAreBuiltThenTheyAreDeterministic()
    {
        var bet = Guid.NewGuid();

        Assert.Equal(DebitId(bet), TransactionId.ForDebit(bet));
        Assert.Equal(CreditId(bet), TransactionId.ForCredit(bet));
        Assert.Equal(RollbackId(bet), TransactionId.ForRollback(bet));
        Assert.Equal(TransactionId.ForDebit(bet), TransactionId.ForDebit(bet));
    }

    [Fact(DisplayName = "Dado um débito de 10,00, quando crédito de 25,00 e estorno do débito (duas vezes), então 3.015,00 após o crédito, +10,00 uma única vez no estorno (3.025,00)")]
    [Trait("Spec", "SPEC-0021:UT-04")]
    public async Task GivenDebitWhenCreditAndRollbackTwiceThenRollbackAppliesOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();
        var bet = Guid.NewGuid();
        AssertOk(await _wallet.DebitAsync(Debit(player, 10.00m, bet), ct), 2_990.00m);

        var credited = await _wallet.CreditAsync(Credit(player, 25.00m, bet), ct);
        var rolledBack = await _wallet.RollbackAsync(Rollback(player, bet), ct);
        var rolledBackAgain = await _wallet.RollbackAsync(Rollback(player, bet), ct);

        AssertOk(credited, 3_015.00m);
        AssertOk(rolledBack, 3_025.00m);
        AssertDuplicateOfOk(rolledBackAgain, 3_025.00m);
        Assert.Equal(BrlBalance(3_025.00m), await _wallet.GetBalanceAsync(player, Brl, ct));

        var entries = _ledger.Entries;
        Assert.Equal(3, entries.Count);
        AssertBalancedEntry(Assert.Single(entries, e => e.TransactionId == DebitId(bet)), player, -10.00m);
        AssertBalancedEntry(Assert.Single(entries, e => e.TransactionId == CreditId(bet)), player, 25.00m);
        var rollback = Assert.Single(entries, e => e.TransactionId == RollbackId(bet));
        AssertBalancedEntry(rollback, player, 10.00m);
        Assert.Equal(DebitId(bet), rollback.OriginalTransactionId);
    }

    [Fact(DisplayName = "Dado nenhum débito da aposta, quando o estorno é pedido, então TransactionNotFound com o id original e nenhum lançamento")]
    [Trait("Spec", "SPEC-0021:UT-04")]
    public async Task GivenNoDebitWhenRollbackIsRequestedThenTransactionNotFoundWithoutEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();
        var bet = Guid.NewGuid();

        var result = await _wallet.RollbackAsync(Rollback(player, bet), ct);

        var notFound = Assert.IsType<WalletTransactionNotFound>(result);
        Assert.Equal(DebitId(bet), notFound.OriginalTransactionId);
        Assert.Empty(_ledger.Entries);
        Assert.Equal(BrlBalance(OpeningBalance), await _wallet.GetBalanceAsync(player, Brl, ct));
    }

    [Fact(DisplayName = "Dada aposta mínima 1,00 e saldo 0,50, quando a recarga é pedida, então o saldo volta a 3.000,00 com um lançamento que soma zero")]
    [Trait("Spec", "SPEC-0021:UT-05")]
    public async Task GivenBalanceBelowMinimumBetWhenRechargingThenBalanceIsThreeThousand()
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();
        AssertOk(await _wallet.DebitAsync(Debit(player, 2_999.50m, Guid.NewGuid()), ct), 0.50m);

        var recharged = await _wallet.RechargeAsync(player, Brl, ct);

        Assert.Equal(BrlBalance(OpeningBalance), recharged);
        Assert.Equal(BrlBalance(OpeningBalance), await _wallet.GetBalanceAsync(player, Brl, ct));
        Assert.Equal(2, _ledger.Entries.Count);
        AssertBalancedEntry(_ledger.Entries[1], player, 2_999.50m);
    }

    [Fact(DisplayName = "Dada aposta mínima 1,00 e saldo 1,00, quando a recarga é pedida, então recusa com RECHARGE_NOT_ALLOWED e nada muda")]
    [Trait("Spec", "SPEC-0021:UT-05")]
    public async Task GivenBalanceAtMinimumBetWhenRechargingThenRechargeNotAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();
        AssertOk(await _wallet.DebitAsync(Debit(player, 2_999.00m, Guid.NewGuid()), ct), 1.00m);

        var error = await Assert.ThrowsAsync<RechargeNotAllowedException>(() => _wallet.RechargeAsync(player, Brl, ct));

        Assert.Equal("RECHARGE_NOT_ALLOWED", error.Code);
        Assert.Equal(BrlBalance(1.00m), await _wallet.GetBalanceAsync(player, Brl, ct));
        Assert.Single(_ledger.Entries);
    }
}
