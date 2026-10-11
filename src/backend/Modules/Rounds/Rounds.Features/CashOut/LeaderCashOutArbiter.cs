using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Features.Conductor;

namespace Apogeu.Rounds.Features.CashOut;

/// <summary>
/// The leader side of <see cref="ICashOutArbiter"/> for one room (SPEC-0023 §4): decides against the crash instant kept in
/// memory by its <see cref="SalaConductor"/> and has the conductor execute the scheduled automatic cash-outs at their target
/// instant while it conducts the flight. The crash point and its instant never leave this instance; only the decision does.
/// </summary>
public sealed class LeaderCashOutArbiter
{
    private readonly SalaConductor _conductor;

    /// <param name="conductor">The conductor of the room led by this instance.</param>
    /// <param name="sink">Receives every automatic cash-out executed.</param>
    public LeaderCashOutArbiter(SalaConductor conductor, IAutoCashOutSink sink)
    {
        ArgumentNullException.ThrowIfNull(conductor);
        ArgumentNullException.ThrowIfNull(sink);
        _conductor = conductor;
        conductor.Attach(sink);
    }

    /// <summary>
    /// Decides a manual cash-out of <paramref name="roundId"/> received at <paramref name="receivedAt"/>:
    /// <see cref="CashOutAccepted"/> with <c>floor(e^(k·(receivedAt − startedAt))·100)/100</c> when
    /// <c>receivedAt &lt; startedAt + t_c</c> (t_c = ln(crash)/k, not rounded), otherwise <see cref="CashOutTooLate"/>.
    /// </summary>
    public CashOutDecision Decide(RoundId roundId, DateTimeOffset receivedAt) => _conductor.Decide(roundId, receivedAt);

    /// <summary>
    /// Schedules the automatic cash-out of <paramref name="betId"/> at <paramref name="target"/> in <paramref name="roundId"/>:
    /// executed at <c>startedAt + ln(target)/k</c> (published to the sink with the exact target) only if that is before the
    /// crash instant; otherwise it never executes.
    /// </summary>
    public void ScheduleAuto(RoundId roundId, Guid betId, Multiplier target) => _conductor.ScheduleAuto(roundId, betId, target);
}
