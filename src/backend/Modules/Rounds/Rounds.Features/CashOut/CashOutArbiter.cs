using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Features.Conductor;

namespace Apogeu.Rounds.Features.CashOut;

/// <summary>
/// <see cref="ICashOutArbiter"/> of this instance (SPEC-0023 §4): the leader of the room decides with its
/// <see cref="LeaderCashOutArbiter"/>; any other instance forwards the request to the leader through Redis
/// (<see cref="CashOutRelay"/>) and gets only the decision back.
/// </summary>
internal sealed class CashOutArbiter(LeaderArbiters arbiters, IRoundsLeadership leadership, CashOutRelay relay) : ICashOutArbiter
{
    public Task<CashOutDecision> DecideAsync(string salaId, RoundId roundId, DateTimeOffset receivedAt, CancellationToken ct) =>
        Local(salaId) is { } leader
            ? leader.DecideAsync(roundId, receivedAt, ct)
            : relay.DecideAsync(salaId, roundId, receivedAt, ct);

    public Task ScheduleAutoAsync(string salaId, RoundId roundId, Guid betId, Multiplier target, CancellationToken ct)
    {
        if (Local(salaId) is not { } leader)
        {
            return relay.ScheduleAutoAsync(salaId, roundId, betId, target, ct);
        }

        leader.ScheduleAuto(roundId, betId, target);
        return Task.CompletedTask;
    }

    /// <summary>The arbiter of the room when this instance leads it and its conductor runs.</summary>
    internal LeaderCashOutArbiter? Local(string salaId) =>
        leadership.IsLeading(salaId) && arbiters.TryGet(salaId, out var leader) ? leader : null;
}
