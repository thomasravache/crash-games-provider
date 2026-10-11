using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Features.Conductor;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Decorator of the Host's <see cref="ICashOutArbiter"/> (SPEC-0023 IT-04, IT-07): records every decision this instance runs
/// and whether it was leading the room at that moment, and — once armed by the test — delays the next decision this instance
/// runs while leading, or holds all of them until released (a leader that does not answer). Seam: the leader decides a forwarded request through the <see cref="ICashOutArbiter"/> of its own
/// container, so this decorator sees the requests other instances forward to it.
/// </summary>
internal sealed class ObservedCashOutArbiter(ICashOutArbiter inner, IRoundsLeadership leadership, ArbiterProbe probe) : ICashOutArbiter
{
    public async Task<CashOutDecision> DecideAsync(string salaId, RoundId roundId, DateTimeOffset receivedAt, CancellationToken ct)
    {
        var leading = leadership.IsLeading(salaId);
        probe.Asked(new ArbiterCall(leading, roundId, receivedAt, Decision: null));
        if (leading && probe.TakeLeaderDelay() is { } delay)
        {
            await Task.Delay(delay, CancellationToken.None).ConfigureAwait(false);
        }

        if (leading && probe.LeaderHold() is { } hold)
        {
            await hold.WaitAsync(TimeSpan.FromSeconds(60), CancellationToken.None).ConfigureAwait(false);
        }

        var decision = await inner.DecideAsync(salaId, roundId, receivedAt, ct).ConfigureAwait(false);
        probe.Record(new ArbiterCall(leading, roundId, receivedAt, decision));
        return decision;
    }

    public Task ScheduleAutoAsync(string salaId, RoundId roundId, Guid betId, Multiplier target, CancellationToken ct) =>
        inner.ScheduleAutoAsync(salaId, roundId, betId, target, ct);
}
