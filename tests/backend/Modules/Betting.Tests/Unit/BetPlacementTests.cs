using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

public sealed class BetPlacementTests
{
    [Fact(DisplayName = "Dada uma rodada em apostas e limites BRL padrão, quando uma aposta de 10,00 chega antes de bettingEndsAt, então fica placed na rodada atual")]
    [Trait("Spec", "SPEC-0022:UT-01")]
    public void GivenBettingRoundWhenBetArrivesBeforeBettingEndsThenItIsPlacedInCurrentRound()
    {
        var round = Bets.Betting(number: 7);
        var request = Bets.Request(amount: 10.00m, panel: 1, autoCashOut: 2.00m);
        var betId = Guid.NewGuid();

        var bet = Bets.Accepted(Bet.Place(betId, Bets.Player, request, Bets.BrlRules, round, Bets.BettingEndsAt.AddMilliseconds(-1), []));

        Assert.Equal(BetStatus.Placed, bet.Status);
        Assert.Equal(betId, bet.Id);
        Assert.Equal(request.ClientBetId, bet.ClientBetId);
        Assert.Equal(round.RoundId, bet.RoundId);
        Assert.Equal(7, bet.RoundNumber);
        Assert.Equal(1, bet.Panel);
        Assert.Equal(10.00m, bet.Amount);
        Assert.Equal(2.00m, bet.AutoCashOut);
        Assert.Equal(Bets.Player, bet.Owner);
    }

    [Fact(DisplayName = "Dado o relógio do servidor exatamente em bettingEndsAt (rodada ainda em apostas), quando a aposta chega, então fica queued para a próxima; quando abre a próxima rodada, então vira placed nela")]
    [Trait("Spec", "SPEC-0022:UT-02")]
    public void GivenServerClockAtBettingEndsWhenBetArrivesThenItIsQueuedAndPlacedInNextRound()
    {
        var current = Bets.Betting(number: 1);

        var bet = Bets.Accepted(Bets.Place(Bets.Request(), current, Bets.BettingEndsAt));

        Assert.Equal(BetStatus.Queued, bet.Status);
        Assert.Null(bet.RoundId);
        Assert.Null(bet.RoundNumber);

        var next = Bets.Betting(number: 2, endsAt: Bets.BettingEndsAt.AddSeconds(20));
        Assert.True(bet.Activate(next), "A queued bet changes when the next round opens its bets.");

        Assert.Equal(BetStatus.Placed, bet.Status);
        Assert.Equal(next.RoundId, bet.RoundId);
        Assert.Equal(2, bet.RoundNumber);
    }

    [Fact(DisplayName = "Dada uma rodada voando, quando a aposta chega, então fica queued; quando abre a próxima rodada, então vira placed nela")]
    [Trait("Spec", "SPEC-0022:UT-02")]
    public void GivenFlyingRoundWhenBetArrivesThenItIsQueuedAndPlacedInNextRound()
    {
        var flying = Bets.Flying(Bets.Betting(number: 1));

        var bet = Bets.Accepted(Bets.Place(Bets.Request(), flying, Bets.BettingEndsAt.AddSeconds(3)));

        Assert.Equal(BetStatus.Queued, bet.Status);
        Assert.Null(bet.RoundId);

        var next = Bets.Betting(number: 2, endsAt: Bets.BettingEndsAt.AddSeconds(20));
        Assert.True(bet.Activate(next));

        Assert.Equal(BetStatus.Placed, bet.Status);
        Assert.Equal(next.RoundId, bet.RoundId);
        Assert.Equal(2, bet.RoundNumber);

        // Activation is applied once: the same opening does not change the bet again.
        Assert.False(bet.Activate(next));
        Assert.Equal(BetStatus.Placed, bet.Status);
    }

    [Fact(DisplayName = "Dado um jogador com aposta no painel 1, quando aposta no painel 2 e de novo no painel 1, então a do painel 2 é aceita e a outra recebe PANEL_BUSY")]
    [Trait("Spec", "SPEC-0022:UT-03")]
    public void GivenBetInPanelOneWhenBettingInPanelTwoAndAgainInPanelOneThenSecondPanelIsAcceptedAndFirstIsBusy()
    {
        var round = Bets.Betting();
        var now = Bets.BettingEndsAt.AddSeconds(-3);
        var first = Bets.Accepted(Bets.Place(Bets.Request(panel: 1), round, now));

        var second = Bets.Accepted(Bets.Place(Bets.Request(amount: 20.00m, panel: 2), round, now, first));
        var again = Bets.Place(Bets.Request(amount: 30.00m, panel: 1), round, now, first, second);

        Assert.Equal(BetStatus.Placed, second.Status);
        Assert.Equal(2, second.Panel);
        Assert.Equal(round.RoundId, second.RoundId);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(BettingErrorCodes.PanelBusy, Bets.Rejected(again));

        // The two bets are independent: the first is unchanged by the second.
        Assert.Equal(BetStatus.Placed, first.Status);
        Assert.Equal(10.00m, first.Amount);
        Assert.Equal(20.00m, second.Amount);
    }

    [Fact(DisplayName = "Dada uma aposta queued no painel 1, quando o jogador faz outra aposta queued no painel 1, então PANEL_BUSY")]
    [Trait("Spec", "SPEC-0022:UT-03")]
    public void GivenQueuedBetInPanelOneWhenAnotherQueuedBetInPanelOneThenPanelBusy()
    {
        var flying = Bets.Flying(Bets.Betting());
        var now = Bets.BettingEndsAt.AddSeconds(2);
        var queued = Bets.Accepted(Bets.Place(Bets.Request(panel: 1), flying, now));

        Assert.Equal(BettingErrorCodes.PanelBusy, Bets.Rejected(Bets.Place(Bets.Request(panel: 1), flying, now, queued)));
        Assert.Equal(BetStatus.Queued, Bets.Accepted(Bets.Place(Bets.Request(panel: 2), flying, now, queued)).Status);
    }

    [Theory(DisplayName = "Dados limites BRL 1,00–1.000,00, quando o valor é validado, então o código de erro esperado")]
    [Trait("Spec", "SPEC-0022:UT-04")]
    [InlineData("0.50", BettingErrorCodes.BetBelowMin)]
    [InlineData("1000.01", BettingErrorCodes.BetAboveMax)]
    [InlineData("1.005", BettingErrorCodes.InvalidAmount)]
    [InlineData("0", BettingErrorCodes.InvalidAmount)]
    [InlineData("-10.00", BettingErrorCodes.InvalidAmount)]
    public void GivenBrlLimitsWhenAmountIsValidatedThenExpectedCode(string amount, string code)
    {
        var placement = Bets.Place(Bets.Request(amount: Parse(amount)), Bets.Betting(), Bets.BettingEndsAt.AddSeconds(-3));

        Assert.Equal(code, Bets.Rejected(placement));
    }

    [Theory(DisplayName = "Dado o limite da sala de 10.000,00x, quando o cash-out automático é 1,00x ou 10.000,01x, então AUTO_CASHOUT_INVALID")]
    [Trait("Spec", "SPEC-0022:UT-04")]
    [InlineData("1.00")]
    [InlineData("10000.01")]
    public void GivenSalaCapWhenAutoCashOutIsOutOfRangeThenAutoCashOutInvalid(string autoCashOut)
    {
        var placement = Bets.Place(Bets.Request(autoCashOut: Parse(autoCashOut)), Bets.Betting(), Bets.BettingEndsAt.AddSeconds(-3));

        Assert.Equal(BettingErrorCodes.AutoCashOutInvalid, Bets.Rejected(placement));
    }

    [Theory(DisplayName = "Dados os limites e o limite da sala, quando valor e cash-out automático estão nas fronteiras válidas, então a aposta é aceita")]
    [Trait("Spec", "SPEC-0022:UT-04")]
    [InlineData("1.00", null)]
    [InlineData("1000.00", null)]
    [InlineData("10.50", "1.01")]
    [InlineData("10.00", "10000.00")]
    public void GivenLimitsWhenValuesAreOnValidBoundariesThenBetIsAccepted(string amount, string? autoCashOut)
    {
        var placement = Bets.Place(
            Bets.Request(amount: Parse(amount), autoCashOut: autoCashOut is null ? null : Parse(autoCashOut)),
            Bets.Betting(),
            Bets.BettingEndsAt.AddSeconds(-3));

        Assert.Equal(BetStatus.Placed, Bets.Accepted(placement).Status);
    }

    [Theory(DisplayName = "Dado um painel diferente de 1 e 2, quando a aposta é validada, então INVALID_PANEL")]
    [Trait("Spec", "SPEC-0022:UT-04")]
    [InlineData(3)]
    [InlineData(0)]
    public void GivenPanelOtherThanOneOrTwoWhenValidatedThenInvalidPanel(int panel)
    {
        var placement = Bets.Place(Bets.Request(panel: panel), Bets.Betting(), Bets.BettingEndsAt.AddSeconds(-3));

        // INVALID_PANEL is new in the contract v3 (emenda 3): literal, so the test compiles before the constant exists.
        Assert.Equal("INVALID_PANEL", Bets.Rejected(placement));
    }

    private static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
