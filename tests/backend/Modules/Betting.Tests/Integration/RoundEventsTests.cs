using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Tests.Support;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class RoundEventsTests(BettingContainers containers)
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Dado o condutor real de uma sala, quando uma rodada passa por apostas, voo e crash e outra é anulada, então o Betting recebe RoundBettingOpened, RoundFlying, RoundCrashed e RoundAnnulled na ordem")]
    [Trait("Spec", "SPEC-0022:IT-05")]
    [Trait("Category", "Integration")]
    public async Task GivenRealConductorWhenRoundsCrashAndAreAnnulledThenBettingReceivesEventsInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var mismatch = new RevealMismatch();
        await using var host = await BettingHost.StartAsync(containers, ct, services: services =>
        {
            services.Decorate<IRoundFeed>((inner, _) => new RecordingRoundFeed(inner));
            services.Decorate<IFairnessService>((inner, _) => new AnnullingFairness(inner, mismatch));
        });
        var feed = Assert.IsType<RecordingRoundFeed>(host.Services.GetRequiredService<IRoundFeed>());
        await using var rounds = await RoundWatch.StartAsync(host, ct);

        // A round played to its crash, entirely seen by Betting.
        var crashedOnHub = await rounds.Recorder.WaitForAsync(
            "RoundCrashed",
            e => rounds.Recorder.Of("RoundBetting", e.RoundId).Count == 1,
            RoundTimeout);
        await Eventually.HoldsAsync(
            () => feed.Received.OfType<RoundCrashed>().Any(c => c.RoundId.Value == crashedOnHub.RoundId),
            TimeSpan.FromSeconds(10),
            () => $"Betting receives RoundCrashed of {crashedOnHub.RoundId}. Received:{Environment.NewLine}{feed.Describe()}");
        var crashedRound = new RoundId(crashedOnHub.RoundId);

        // The next round revealed is annulled by the real conductor.
        mismatch.ArmNextReveal();
        var annulled = await Eventually.ResultAsync(
            () => Task.FromResult(feed.Received.OfType<RoundAnnulled>().FirstOrDefault()),
            _ => true,
            RoundTimeout,
            () => $"Betting receives a RoundAnnulled. Received:{Environment.NewLine}{feed.Describe()}");

        var received = feed.Received;
        var log = feed.Describe();
        AssertLifecycle<RoundCrashed>(received, crashedRound, log);
        AssertLifecycle<RoundAnnulled>(received, annulled.RoundId, log);

        var opened = received.OfType<RoundBettingOpened>().Single(e => e.RoundId == crashedRound);
        var nextOpened = received.OfType<RoundBettingOpened>().Single(e => e.RoundId == annulled.RoundId);
        var flying = received.OfType<RoundFlying>().Single(e => e.RoundId == crashedRound);
        var crashed = received.OfType<RoundCrashed>().Single(e => e.RoundId == crashedRound);
        Assert.All((string[])[opened.SalaId, nextOpened.SalaId, flying.SalaId, crashed.SalaId, annulled.SalaId], sala => Assert.Equal(BettingSala.SalaId, sala));
        Assert.True(nextOpened.RoundNumber > opened.RoundNumber, $"The annulled round comes after the crashed one.{Environment.NewLine}{log}");
        Assert.Equal(Json.Instant(rounds.BettingOf(crashedRound.Value).Payload, "bettingEndsAt"), opened.BettingEndsAt);
        Assert.Equal(0.0002m, flying.GrowthK);
        Assert.Equal(Json.Decimal(crashedOnHub.Payload, "crashPoint"), crashed.CrashPoint);
        Assert.Equal(Json.Instant(crashedOnHub.Payload, "crashedAt"), crashed.CrashedAt);
        Assert.Equal("CRASH_POINT_MISMATCH", annulled.Reason);
        Assert.DoesNotContain(received.OfType<RoundCrashed>(), c => c.RoundId == annulled.RoundId);
    }

    /// <summary>The round was opened, then flew, then ended with <typeparamref name="TEnd"/>, each event received once and in that order.</summary>
    private static void AssertLifecycle<TEnd>(IReadOnlyList<object> received, RoundId round, string log)
    {
        var opened = Index(received, e => e is RoundBettingOpened o && o.RoundId == round, "RoundBettingOpened", round, log);
        var flying = Index(received, e => e is RoundFlying f && f.RoundId == round, "RoundFlying", round, log);
        var ended = Index(received, e => e is TEnd && RoundOf(e) == round, typeof(TEnd).Name, round, log);
        Assert.True(opened < flying && flying < ended, $"Expected RoundBettingOpened, RoundFlying and {typeof(TEnd).Name} of {round.Value} in this order. Received:{Environment.NewLine}{log}");
    }

    private static int Index(IReadOnlyList<object> received, Func<object, bool> match, string name, RoundId round, string log)
    {
        var positions = received.Select((e, i) => (e, i)).Where(x => match(x.e)).Select(x => x.i).ToList();
        Assert.True(positions.Count == 1, $"Expected one {name} of {round.Value}, got {positions.Count}. Received:{Environment.NewLine}{log}");
        return positions[0];
    }

    private static RoundId? RoundOf(object received) => received switch
    {
        RoundCrashed c => c.RoundId,
        RoundAnnulled a => a.RoundId,
        _ => null,
    };
}
