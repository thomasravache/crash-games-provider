using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

public sealed class PanelOccupancyTests
{
    [Fact(DisplayName = "Dada uma aposta queued no painel 1 ainda não ativada, quando abre a rodada seguinte e chega outra aposta no painel 1 para a rodada atual, então PANEL_BUSY")]
    [Trait("Spec", "SPEC-0022:UT-06")]
    public void GivenQueuedBetInPanelOneWhenAnotherBetForTheOpenRoundArrivesThenPanelBusy()
    {
        var flying = Bets.Flying(Bets.Betting(number: 1));
        var queued = Bets.Accepted(Bets.Place(Bets.Request(panel: 1), flying, Bets.BettingEndsAt.AddSeconds(2)));
        Assert.Equal(BetStatus.Queued, queued.Status);

        // The next round opened, but the queued bet has not been activated yet (the race of IT-07).
        var next = Bets.Betting(number: 2, endsAt: Bets.BettingEndsAt.AddSeconds(20));
        var placement = Bets.Place(Bets.Request(amount: 30.00m, panel: 1), next, Bets.BettingEndsAt.AddSeconds(15), queued);

        Assert.Equal(BettingErrorCodes.PanelBusy, Bets.Rejected(placement));

        // The queued bet keeps its panel and is activated into the round normally.
        Assert.True(queued.Activate(next));
        Assert.Equal(BetStatus.Placed, queued.Status);
    }

    [Fact(DisplayName = "Dada uma aposta queued no painel 1, quando chega outra aposta no painel 1 para a próxima rodada, então PANEL_BUSY")]
    [Trait("Spec", "SPEC-0022:UT-06")]
    public void GivenQueuedBetInPanelOneWhenAnotherQueuedBetArrivesThenPanelBusy()
    {
        var flying = Bets.Flying(Bets.Betting(number: 1));
        var now = Bets.BettingEndsAt.AddSeconds(2);
        var queued = Bets.Accepted(Bets.Place(Bets.Request(panel: 1), flying, now));

        Assert.Equal(BettingErrorCodes.PanelBusy, Bets.Rejected(Bets.Place(Bets.Request(amount: 30.00m, panel: 1), flying, now, queued)));
    }
}
