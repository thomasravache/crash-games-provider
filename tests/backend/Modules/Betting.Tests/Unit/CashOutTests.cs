using Apogeu.Betting.Domain;
using Apogeu.Betting.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

/// <summary>The payout and the cash-out transition of a bet (SPEC-0023 §6), on the domain only.</summary>
public sealed class CashOutTests
{
    /// <summary>The max win of the demo operator in BRL (SPEC-0020 seed).</summary>
    private const decimal MaxWin = 100_000.00m;

    [Theory(DisplayName = "Dados valor e multiplicador, quando o pagamento é calculado, então é truncado em centavos e limitado ao ganho máximo")]
    [Trait("Spec", "SPEC-0023:UT-02")]
    [InlineData("10.00", "2.47", "24.70")]
    [InlineData("3.33", "1.07", "3.56")]
    [InlineData("1000.00", "150.00", "100000.00")]
    [InlineData("1.00", "1.01", "1.01")]
    [InlineData("7.77", "1.99", "15.46")]
    public void GivenAmountAndMultiplierWhenPayoutIsComputedThenItIsTruncatedAndCappedByMaxWin(string amount, string multiplier, string payout)
    {
        Assert.Equal(Parse(payout), Bet.PayoutOf(Parse(amount), Parse(multiplier), MaxWin));
    }

    [Fact(DisplayName = "Dada uma aposta active de 1.000,00, quando retirada em 150,00x, então o pagamento é o ganho máximo 100.000,00")]
    [Trait("Spec", "SPEC-0023:UT-02")]
    public void GivenActiveBetWhenCashedOutAboveMaxWinThenPayoutIsMaxWin()
    {
        var bet = Active(1000.00m);

        var accepted = Assert.IsType<BetCashOutAccepted>(bet.CashOut(150.00m, MaxWin));

        Assert.Equal(150.00m, accepted.Multiplier);
        Assert.Equal(100_000.00m, accepted.Payout);
        Assert.Equal(BetStatus.CashedOut, bet.Status);
    }

    [Fact(DisplayName = "Dadas apostas placed, queued, cancelled e já retirada, quando CashOut é pedido, então BET_NOT_ACTIVE e a já retirada devolve o CashedOut original")]
    [Trait("Spec", "SPEC-0023:UT-04")]
    public void GivenPlacedQueuedCancelledAndCashedOutBetsWhenCashOutIsRequestedThenNotActiveAndOriginalResult()
    {
        var round = Bets.Betting(number: 1);
        var duringBets = Bets.BettingEndsAt.AddSeconds(-3);

        var placed = Bets.Accepted(Bets.Place(Bets.Request(amount: 10.00m, panel: 1), round, duringBets));
        AssertNotActive(placed, BetStatus.Placed);

        var queued = Bets.Accepted(Bets.Place(Bets.Request(amount: 10.00m, panel: 1), Bets.Flying(round), Bets.BettingEndsAt.AddSeconds(1)));
        AssertNotActive(queued, BetStatus.Queued);

        var cancelled = Bets.Accepted(Bets.Place(Bets.Request(amount: 10.00m, panel: 2), round, duringBets));
        Assert.IsType<BetCancellationAccepted>(cancelled.Cancel(round, duringBets.AddSeconds(1)));
        AssertNotActive(cancelled, BetStatus.Cancelled);

        // Cashed out in this process: a second cash-out answers the first one, unchanged.
        var active = Active(10.00m);
        var first = Assert.IsType<BetCashOutAccepted>(active.CashOut(1.50m, MaxWin));
        Assert.Equal(new BetCashOutAccepted(1.50m, 15.00m), first);
        Assert.Equal(BetStatus.CashedOut, active.Status);
        Assert.Equal(1.50m, active.CashOutMultiplier);
        Assert.Equal(15.00m, active.Payout);
        Assert.Equal(first, active.CashOut(1.80m, MaxWin));
        Assert.Equal(1.50m, active.CashOutMultiplier);

        // Cashed out and restored from persistence: the original result too.
        var restored = Bet.Restore(
            Guid.NewGuid(),
            Bets.Player,
            Bets.Request(amount: 10.00m, panel: 2),
            BetStatus.CashedOut,
            round.RoundId,
            round.RoundNumber,
            cashOutMultiplier: 2.00m,
            payout: 20.00m);
        Assert.Equal(new BetCashOutAccepted(2.00m, 20.00m), restored.CashOut(3.00m, MaxWin));
        Assert.Equal(BetStatus.CashedOut, restored.Status);
    }

    private static void AssertNotActive(Bet bet, BetStatus status)
    {
        var rejected = Assert.IsType<BetCashOutRejected>(bet.CashOut(1.50m, MaxWin));
        Assert.Equal("BET_NOT_ACTIVE", rejected.Code);
        Assert.Equal(status, bet.Status);
        Assert.Null(bet.CashOutMultiplier);
        Assert.Null(bet.Payout);
    }

    private static Bet Active(decimal amount)
    {
        var round = Bets.Betting(number: 1);
        var bet = Bets.Accepted(Bets.Place(Bets.Request(amount: amount, panel: 1), round, Bets.BettingEndsAt.AddSeconds(-3)));
        bet.StartFlight(round.RoundId);
        Assert.Equal(BetStatus.Active, bet.Status);
        return bet;
    }

    private static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
