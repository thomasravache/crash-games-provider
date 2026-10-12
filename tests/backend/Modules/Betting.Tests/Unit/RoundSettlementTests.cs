using Apogeu.Betting.Domain;
using Apogeu.Betting.Tests.Support;
using Apogeu.BuildingBlocks.Contracts;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

/// <summary>The closing of a round's bets and its summary (SPEC-0024 §6), on the domain only.</summary>
public sealed class RoundSettlementTests
{
    private const decimal MaxWin = 100_000.00m;

    [Fact(DisplayName = "Dada uma rodada com 3 apostas active (1 retirada), quando o crash é processado, então 2 viram lost e a retirada não muda")]
    [Trait("Spec", "SPEC-0024:UT-01")]
    public void GivenThreeActiveBetsOneCashedOutWhenCrashIsProcessedThenTwoAreLostAndTheCashedOutIsUnchanged()
    {
        var round = Bets.Betting(number: 1);
        var cashedOut = Active(round, 10.00m, panel: 1);
        Assert.IsType<BetCashOutAccepted>(cashedOut.CashOut(2.47m, MaxWin));
        var first = Active(round, 20.00m, panel: 2);
        var second = Active(round, 30.00m, panel: 1, Bets.Player with { PlayerId = new PlayerId("player-2") });

        var closures = new[] { cashedOut, first, second }.Select(bet => bet.Settle()).ToList();

        Assert.Equal(new BetClosureSkipped(BetStatus.CashedOut), closures[0]);
        Assert.IsType<BetClosedLost>(closures[1]);
        Assert.IsType<BetClosedLost>(closures[2]);
        Assert.Equal(BetStatus.CashedOut, cashedOut.Status);
        Assert.Equal(2.47m, cashedOut.CashOutMultiplier);
        Assert.Equal(24.70m, cashedOut.Payout);
        Assert.Equal(BetStatus.Lost, first.Status);
        Assert.Equal(BetStatus.Lost, second.Status);

        // Settling again changes nothing (a crash processed twice).
        Assert.Equal(new BetClosureSkipped(BetStatus.Lost), first.Settle());
        Assert.Equal(BetStatus.Lost, first.Status);
    }

    [Fact(DisplayName = "Dada uma rodada com apostas placed, active e uma retirada, quando a anulação é processada, então placed e active viram refunded com estorno integral e a retirada não muda")]
    [Trait("Spec", "SPEC-0024:UT-02")]
    public void GivenPlacedActiveAndCashedOutBetsWhenAnnulmentIsProcessedThenPlacedAndActiveAreRefundedInFull()
    {
        var round = Bets.Betting(number: 1);
        var placed = Bets.Accepted(Bets.Place(Bets.Request(amount: 15.00m, panel: 1), round, Bets.BettingEndsAt.AddSeconds(-3)));
        Assert.Equal(BetStatus.Placed, placed.Status);
        var active = Active(round, 20.00m, panel: 2);
        var cashedOut = Active(round, 10.00m, panel: 1, Bets.Player with { PlayerId = new PlayerId("player-2") });
        Assert.IsType<BetCashOutAccepted>(cashedOut.CashOut(1.50m, MaxWin));

        Assert.Equal(new BetClosedRefunded(15.00m), placed.Refund());
        Assert.Equal(new BetClosedRefunded(20.00m), active.Refund());
        Assert.Equal(new BetClosureSkipped(BetStatus.CashedOut), cashedOut.Refund());

        Assert.Equal(BetStatus.Refunded, placed.Status);
        Assert.Equal(BetStatus.Refunded, active.Status);
        Assert.Equal(BetStatus.CashedOut, cashedOut.Status);
        Assert.Equal(15.00m, cashedOut.Payout);

        // Refunding again changes nothing (an annulment processed twice).
        Assert.Equal(new BetClosureSkipped(BetStatus.Refunded), active.Refund());
    }

    [Fact(DisplayName = "Dadas apostas de 10,00, 20,00 e 30,00 com um pagamento de 24,70, quando o resumo é calculado, então apostado 60,00, pago 24,70 e GGR 35,30")]
    [Trait("Spec", "SPEC-0024:UT-03")]
    public void GivenThreeBetsAndOnePayoutWhenSummarizedThenWageredPaidAndGgr()
    {
        var round = Bets.Betting(number: 1);
        var cashedOut = Active(round, 10.00m, panel: 1);
        Assert.IsType<BetCashOutAccepted>(cashedOut.CashOut(2.47m, MaxWin));
        var lost = Active(round, 20.00m, panel: 2);
        Assert.IsType<BetClosedLost>(lost.Settle());
        var other = Active(round, 30.00m, panel: 1, Bets.Player with { PlayerId = new PlayerId("player-2") });
        Assert.IsType<BetClosedLost>(other.Settle());

        var summary = Assert.Single(RoundSettlementCalculator.Summarize([cashedOut, lost, other]));

        Assert.Equal(new RoundSettlementSummary(new OperatorId("demo"), new Currency("BRL"), 3, 60.00m, 24.70m, 0.00m, 35.30m), summary);
    }

    [Fact(DisplayName = "Dada uma rodada anulada com uma aposta estornada de 10,00 e uma retirada de 20,00 paga 30,00, quando o resumo é calculado, então estornado 10,00 e GGR = apostado − pago − estornado")]
    [Trait("Spec", "SPEC-0024:UT-03")]
    public void GivenRefundedAndCashedOutBetsWhenSummarizedThenRefundedEntersTheGgr()
    {
        var round = Bets.Betting(number: 1);
        var refunded = Active(round, 10.00m, panel: 1);
        Assert.IsType<BetClosedRefunded>(refunded.Refund());
        var cashedOut = Active(round, 20.00m, panel: 2);
        Assert.IsType<BetCashOutAccepted>(cashedOut.CashOut(1.50m, MaxWin));

        var summary = Assert.Single(RoundSettlementCalculator.Summarize([refunded, cashedOut]));

        Assert.Equal(new RoundSettlementSummary(new OperatorId("demo"), new Currency("BRL"), 2, 30.00m, 30.00m, 10.00m, -10.00m), summary);
    }

    private static Bet Active(RoundWindow round, decimal amount, int panel, BetOwner? owner = null)
    {
        var bet = Bets.Accepted(Bet.Place(
            Guid.NewGuid(),
            owner ?? Bets.Player,
            Bets.Request(amount: amount, panel: panel),
            Bets.BrlRules,
            round,
            Bets.BettingEndsAt.AddSeconds(-3),
            []));
        bet.StartFlight(round.RoundId);
        Assert.Equal(BetStatus.Active, bet.Status);
        return bet;
    }
}
