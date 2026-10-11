using Apogeu.Betting.Domain;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.TestKit.Time;
using Xunit;

namespace Apogeu.Betting.Tests.Support;

/// <summary>Builders of the unit tests: the BRL limits of the demo operator (SPEC-0020: 1,00 / 1.000,00) and a room cap of 10.000,00x.</summary>
internal static class Bets
{
    public static readonly BetOwner Player = new(new OperatorId("demo"), new PlayerId("player-1"), new Currency("BRL"));

    public static readonly BetRules BrlRules = new(MinBet: 1.00m, MaxBet: 1000.00m, MaxAutoCashOut: 10000.00m);

    /// <summary>The server instant the betting phase of <see cref="Betting"/> ends.</summary>
    public static readonly DateTimeOffset BettingEndsAt = TestClock.Epoch.AddSeconds(6);

    public static RoundWindow Betting(long number = 1, DateTimeOffset? endsAt = null) =>
        new(new RoundId(Guid.CreateVersion7()), number, endsAt ?? BettingEndsAt, RoundPhase.Betting);

    public static RoundWindow Flying(RoundWindow round) => round with { Phase = RoundPhase.Flying };

    public static BetRequest Request(decimal amount = 10.00m, int panel = 1, decimal? autoCashOut = null) =>
        new(Guid.NewGuid(), panel, amount, autoCashOut);

    public static BetPlacement Place(BetRequest request, RoundWindow? current, DateTimeOffset now, params Bet[] openBets) =>
        Bet.Place(Guid.NewGuid(), Player, request, BrlRules, current, now, openBets);

    public static Bet Accepted(BetPlacement placement)
    {
        var accepted = Assert.IsType<BetPlacementAccepted>(placement);
        return accepted.Bet;
    }

    public static string Rejected(BetPlacement placement)
    {
        var rejected = Assert.IsType<BetPlacementRejected>(placement);
        return rejected.Code;
    }
}
