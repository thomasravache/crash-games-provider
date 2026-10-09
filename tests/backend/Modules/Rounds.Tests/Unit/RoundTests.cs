using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.TestKit.Time;
using Xunit;

namespace Apogeu.Rounds.Tests.Unit;

public sealed class RoundTests
{
    private const string Commitment = "8498797e345294e1c161ff895a7a12305502f4410185c65171e387a40bf99daf";
    private const string RevealedSeed = "e2e2f30e357b0e33ff1102bd873f7b10299418647d96615c3e7475d951f7128f";

    [Fact(DisplayName = "Dada uma rodada em Betting, quando as transições válidas ocorrem, então chega a Crashed; dada uma rodada Crashed, quando se tenta iniciar voo, então lança erro de domínio")]
    [Trait("Spec", "SPEC-0008:UT-01")]
    public void GivenBettingRoundWhenValidTransitionsOccurThenItCrashesAndCrashedRoundCannotFly()
    {
        var bettingEndsAt = TestClock.Epoch.AddSeconds(6);
        var startedAt = bettingEndsAt;
        var crashedAt = startedAt.AddMilliseconds(11_553);
        var id = new RoundId(Guid.NewGuid());

        var round = Round.OpenBetting(id, "demo", number: 1, chainId: Guid.NewGuid(), chainRoundIndex: 1, Commitment, bettingEndsAt);

        Assert.Equal(RoundState.Betting, round.State);
        Assert.Equal(id, round.Id);
        Assert.Equal(1, round.Number);
        Assert.Equal(Commitment, round.Commitment);
        Assert.Null(round.CrashPoint);
        Assert.Null(round.RevealedSeed);

        round.StartFlight(startedAt);

        Assert.Equal(RoundState.Flying, round.State);
        Assert.Equal(startedAt, round.StartedAt);
        Assert.Null(round.CrashPoint);
        Assert.Null(round.RevealedSeed);

        round.Crash(2.00m, RevealedSeed, crashedAt);

        Assert.Equal(RoundState.Crashed, round.State);
        Assert.Equal(2.00m, round.CrashPoint);
        Assert.Equal(RevealedSeed, round.RevealedSeed);
        Assert.Equal(crashedAt, round.CrashedAt);

        Assert.Throws<InvalidRoundTransitionException>(() => round.StartFlight(crashedAt.AddSeconds(1)));
        Assert.Equal(RoundState.Crashed, round.State);
    }

    [Fact(DisplayName = "Dada uma rodada em Betting, quando se tenta o crash antes do voo, então lança erro de domínio e continua em Betting")]
    [Trait("Spec", "SPEC-0008:UT-01")]
    public void GivenBettingRoundWhenCrashedBeforeFlightThenDomainErrorIsThrown()
    {
        var round = Round.OpenBetting(new RoundId(Guid.NewGuid()), "demo", 1, Guid.NewGuid(), 1, Commitment, TestClock.Epoch.AddSeconds(6));

        Assert.Throws<InvalidRoundTransitionException>(() => round.Crash(2.00m, RevealedSeed, TestClock.Epoch.AddSeconds(7)));
        Assert.Equal(RoundState.Betting, round.State);
        Assert.Null(round.CrashPoint);
    }
}
