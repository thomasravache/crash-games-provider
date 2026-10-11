using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

public sealed class BetCancellationTests
{
    [Fact(DisplayName = "Dadas apostas placed, queued e active, quando canceladas, então as duas primeiras ficam cancelled com estorno integral e a active recebe CANCEL_NOT_ALLOWED")]
    [Trait("Spec", "SPEC-0022:UT-05")]
    public void GivenPlacedQueuedAndActiveBetsWhenCancelledThenFirstTwoAreRefundedAndActiveIsNotAllowed()
    {
        var round = Bets.Betting(number: 1);
        var duringBets = Bets.BettingEndsAt.AddSeconds(-3);
        var placed = Bets.Accepted(Bets.Place(Bets.Request(amount: 10.00m, panel: 1), round, duringBets));
        var active = Bets.Accepted(Bets.Place(Bets.Request(amount: 25.00m, panel: 1), round, duringBets));
        Assert.Equal(BetStatus.Placed, placed.Status);

        // Placed, in the betting phase of its round.
        var cancelled = Assert.IsType<BetCancellationAccepted>(placed.Cancel(round, duringBets.AddSeconds(1)));
        Assert.Equal(10.00m, cancelled.Refunded);
        Assert.Equal(BetStatus.Cancelled, placed.Status);

        // Active: the flight of its round started.
        var flying = Bets.Flying(round);
        active.StartFlight(round.RoundId);
        Assert.Equal(BetStatus.Active, active.Status);
        var refused = Assert.IsType<BetCancellationRejected>(active.Cancel(flying, Bets.BettingEndsAt.AddSeconds(1)));
        Assert.Equal(BettingErrorCodes.CancelNotAllowed, refused.Code);
        Assert.Equal(BetStatus.Active, active.Status);

        // Queued, made during the flight: cancelled while still waiting for the next round.
        var queued = Bets.Accepted(Bets.Place(Bets.Request(amount: 7.50m, panel: 2), flying, Bets.BettingEndsAt.AddSeconds(2)));
        Assert.Equal(BetStatus.Queued, queued.Status);
        var queuedCancelled = Assert.IsType<BetCancellationAccepted>(queued.Cancel(flying, Bets.BettingEndsAt.AddSeconds(3)));
        Assert.Equal(7.50m, queuedCancelled.Refunded);
        Assert.Equal(BetStatus.Cancelled, queued.Status);
    }
}
