using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Rounds.Contracts;

/// <summary>
/// Decides cash-outs of a room (SPEC-0023 §6, contract v2): only the leader of the room knows the crash instant (ADR-0014),
/// so an instance that does not lead it forwards the request to the leader (Redis request/response with a deadline). Only
/// the decision leaves the leader, never the crash point nor its instant.
/// </summary>
public interface ICashOutArbiter
{
    /// <summary>
    /// Decides a manual cash-out of <paramref name="roundId"/> received at <paramref name="receivedAt"/> (server clock,
    /// latency tolerance 0): <see cref="CashOutAccepted"/> with <c>floor(e^(k·(receivedAt − startedAt))·100)/100</c> when
    /// <c>receivedAt &lt; startedAt + t_c</c>, otherwise <see cref="CashOutTooLate"/>.
    /// </summary>
    Task<CashOutDecision> DecideAsync(string salaId, RoundId roundId, DateTimeOffset receivedAt, CancellationToken ct);

    /// <summary>
    /// Schedules the automatic cash-out of bet <paramref name="betId"/> at <paramref name="target"/>: the leader executes it
    /// at <c>t_alvo = ln(target)/k</c> only if <c>t_alvo &lt; t_c</c>, publishing <see cref="AutoCashOutExecuted"/>.
    /// </summary>
    Task ScheduleAutoAsync(string salaId, RoundId roundId, Guid betId, Multiplier target, CancellationToken ct);
}
