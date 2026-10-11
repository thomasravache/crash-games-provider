using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.Conductor;

namespace Apogeu.Rounds.Features.CashOut;

/// <summary>
/// The leader side of <see cref="ICashOutArbiter"/> for one room (SPEC-0023 §4): decides against the crash instant kept in
/// memory by its <see cref="SalaConductor"/> — or, for a round out of that memory, from the round as persisted — and has the
/// conductor execute the scheduled automatic cash-outs at their target instant while it conducts the flight. The crash point
/// and its instant never leave this instance; only the decision does.
/// </summary>
public sealed class LeaderCashOutArbiter
{
    private readonly SalaConductor _conductor;
    private readonly Func<RoundId, CancellationToken, Task<Round?>> _findRound;

    /// <param name="conductor">The conductor of the room led by this instance; its store answers the rounds out of its memory.</param>
    /// <param name="sink">Receives every automatic cash-out executed.</param>
    public LeaderCashOutArbiter(SalaConductor conductor, IAutoCashOutSink sink)
        : this(conductor, sink, findRound: null)
    {
    }

    /// <param name="conductor">The conductor of the room led by this instance.</param>
    /// <param name="sink">Receives every automatic cash-out executed.</param>
    /// <param name="findRound">Reads a persisted round safely from any thread (the Host's); the conductor's store when null.</param>
    internal LeaderCashOutArbiter(SalaConductor conductor, IAutoCashOutSink sink, Func<RoundId, CancellationToken, Task<Round?>>? findRound)
    {
        ArgumentNullException.ThrowIfNull(conductor);
        ArgumentNullException.ThrowIfNull(sink);
        _conductor = conductor;
        _findRound = findRound ?? conductor.FindRoundAsync;
        conductor.Attach(sink);
    }

    /// <summary>
    /// Decides a manual cash-out of <paramref name="roundId"/> received at <paramref name="receivedAt"/> from the flight in
    /// memory: <see cref="CashOutAccepted"/> with <c>floor(e^(k·(receivedAt − startedAt))·100)/100</c> when
    /// <c>receivedAt &lt; startedAt + t_c</c> (t_c = ln(crash)/k, not rounded), <see cref="CashOutTooLate"/> otherwise, and
    /// <see cref="CashOutUndecided"/> for a round not in memory.
    /// </summary>
    public CashOutDecision Decide(RoundId roundId, DateTimeOffset receivedAt) => _conductor.Decide(roundId, receivedAt);

    /// <summary>
    /// As <see cref="Decide"/>, and for a round not in memory from the round as persisted: a crashed one with the same rule
    /// from its <c>startedAt</c> and crash point; one not ended (or unknown) stays <see cref="CashOutUndecided"/>.
    /// </summary>
    public async Task<CashOutDecision> DecideAsync(RoundId roundId, DateTimeOffset receivedAt, CancellationToken ct)
    {
        var decision = _conductor.Decide(roundId, receivedAt);
        if (decision is not CashOutUndecided)
        {
            return decision;
        }

        var round = await _findRound(roundId, ct).ConfigureAwait(false);
        return _conductor.DecidePersisted(round, receivedAt);
    }

    /// <summary>
    /// Schedules the automatic cash-out of <paramref name="betId"/> at <paramref name="target"/> in <paramref name="roundId"/>:
    /// executed at <c>startedAt + ln(target)/k</c> (published to the sink with the exact target) only if that is before the
    /// crash instant; otherwise it never executes.
    /// </summary>
    public void ScheduleAuto(RoundId roundId, Guid betId, Multiplier target) => _conductor.ScheduleAuto(roundId, betId, target);
}
