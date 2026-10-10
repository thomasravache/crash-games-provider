using System.Diagnostics;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Apogeu.Wallet.Infrastructure.Demo;
using Apogeu.Wallet.Infrastructure.Observability;
using Apogeu.Wallet.Tests.Support;
using Npgsql;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Unit;

/// <summary>
/// UT-06 and UT-07 (review G4 of SPEC-0021): amounts outside the cent scale are refused at the port, a debit is reversed at
/// most once whatever the rollback id, and a transient database failure becomes <see cref="WalletUnavailable"/> with an
/// errored trace.
/// </summary>
public sealed class DemoWalletGuardTests
{
    public static TheoryData<string> InvalidAmounts => ["10.005", "0.001"];

    public static TheoryData<string> TransientFailures => ["npgsql", "timeout"];

    private readonly InMemoryWalletLedger _ledger = new();
    private readonly DemoWallet _wallet;

    public DemoWalletGuardTests() => _wallet = new DemoWallet(_ledger, new FixedOperatorLimits(), TimeProvider.System);

    [Theory(DisplayName = "Dado um débito com valor fora da escala de centavos, quando chega à porta, então ArgumentOutOfRangeException sem lançamento")]
    [MemberData(nameof(InvalidAmounts))]
    [Trait("Spec", "SPEC-0021:UT-06")]
    public async Task GivenDebitWithSubCentAmountWhenItReachesThePortThenArgumentOutOfRange(string amount)
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _wallet.DebitAsync(Debit(player, Parse(amount), Guid.NewGuid()), ct));

        Assert.Empty(_ledger.Entries);
        Assert.Equal(BrlBalance(OpeningBalance), await _wallet.GetBalanceAsync(player, Brl, ct));
    }

    [Theory(DisplayName = "Dado um crédito com valor fora da escala de centavos, quando chega à porta, então ArgumentOutOfRangeException sem lançamento")]
    [MemberData(nameof(InvalidAmounts))]
    [Trait("Spec", "SPEC-0021:UT-06")]
    public async Task GivenCreditWithSubCentAmountWhenItReachesThePortThenArgumentOutOfRange(string amount)
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _wallet.CreditAsync(Credit(player, Parse(amount), Guid.NewGuid()), ct));

        Assert.Empty(_ledger.Entries);
        Assert.Equal(BrlBalance(OpeningBalance), await _wallet.GetBalanceAsync(player, Brl, ct));
    }

    [Theory(DisplayName = "Dado um débito gravado com valor fora da escala de centavos, quando o estorno chega à porta, então ArgumentOutOfRangeException sem lançamento")]
    [MemberData(nameof(InvalidAmounts))]
    [Trait("Spec", "SPEC-0021:UT-06")]
    public async Task GivenRecordedSubCentDebitWhenRollbackReachesThePortThenArgumentOutOfRange(string amount)
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();
        var bet = Guid.NewGuid();

        // A rollback carries no amount: it gives back the original debit, recorded here straight in the ledger double
        // with an amount the port would never accept (the adapter must not post it back).
        var value = Parse(amount);
        var account = LedgerAccount.OfPlayer(player, Brl);
        var seeded = new LedgerEntry(
            DebitId(bet),
            LedgerEntryKind.Debit,
            player,
            Brl,
            RoundId: null,
            bet,
            OriginalTransactionId: null,
            [new LedgerPosting(account, new Money(-value)), new LedgerPosting(LedgerAccount.OfHouse(Demo, Brl), new Money(value))],
            new Money(OpeningBalance - value),
            DateTimeOffset.UtcNow);
        Assert.Equal(LedgerAppendOutcome.Appended, await _ledger.TryAppendAsync(seeded, new Money(0m), ct));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _wallet.RollbackAsync(Rollback(player, bet), ct));

        Assert.Single(_ledger.Entries);
    }

    [Fact(DisplayName = "Dado um débito de 10,00, quando chegam dois estornos com TransactionIds diferentes para o mesmo débito, então devolve uma única vez e o segundo é Duplicate do primeiro")]
    [Trait("Spec", "SPEC-0021:UT-06")]
    public async Task GivenDebitWhenTwoRollbacksWithDifferentIdsArriveThenRefundOnceAndSecondIsDuplicate()
    {
        var ct = TestContext.Current.CancellationToken;
        var player = NewPlayer();
        var bet = Guid.NewGuid();
        AssertOk(await _wallet.DebitAsync(Debit(player, 10.00m, bet), ct), 2_990.00m);
        var first = Rollback(player, bet);
        var second = first with { TransactionId = new TransactionId("bet:" + bet.ToString("D") + ":rollback:retry") };

        var firstResult = await _wallet.RollbackAsync(first, ct);
        var secondResult = await _wallet.RollbackAsync(second, ct);

        AssertOk(firstResult, OpeningBalance);
        AssertDuplicateOfOk(secondResult, OpeningBalance);
        Assert.Equal(BrlBalance(OpeningBalance), await _wallet.GetBalanceAsync(player, Brl, ct));
        Assert.Equal(2, _ledger.Entries.Count);
        Assert.Single(_ledger.Entries, entry => entry.Kind == LedgerEntryKind.Rollback);
    }

    [Theory(DisplayName = "Dada uma falha transitória do banco durante um débito, quando a operação é executada, então WalletUnavailable(retryable: true) e a Activity com status Error")]
    [MemberData(nameof(TransientFailures))]
    [Trait("Spec", "SPEC-0021:UT-07")]
    public async Task GivenTransientDatabaseFailureWhenDebitingThenUnavailableRetryableAndErroredTrace(string failure)
    {
        var ct = TestContext.Current.CancellationToken;
        Func<Exception> fault = string.Equals(failure, "npgsql"
, StringComparison.Ordinal) ? () => new NpgsqlException("Exception while connecting (test).")
            : () => new TimeoutException("Timeout during reading attempt (test).");
        var wallet = new DemoWallet(new FailingWalletLedger(fault), new FixedOperatorLimits(), TimeProvider.System);
        var debit = Debit(NewPlayer(), 10.00m, Guid.NewGuid());
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, WalletMetrics.MeterName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (string.Equals(activity.GetTagItem("wallet.transaction_id") as string, debit.TransactionId.Value, StringComparison.Ordinal))
                {
                    lock (stopped)
                    {
                        stopped.Add(activity);
                    }
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        var result = await wallet.DebitAsync(debit, ct);

        var unavailable = Assert.IsType<WalletUnavailable>(result);
        Assert.True(unavailable.Retryable);
        Activity activity;
        lock (stopped)
        {
            activity = Assert.Single(stopped);
        }

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    private static decimal Parse(string amount) => decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
}
