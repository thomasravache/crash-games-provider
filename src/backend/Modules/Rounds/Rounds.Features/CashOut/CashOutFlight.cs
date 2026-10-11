using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;

namespace Apogeu.Rounds.Features.CashOut;

/// <summary>
/// A flight as the leader decides its cash-outs (SPEC-0023): its start, the crash instant <c>startedAt + t_c</c> (not rounded)
/// and its crash point, and whether it already ended. In memory of the leader only, never sent nor logged (ADR-0014).
/// </summary>
internal sealed record CashOutFlight(RoundId RoundId, DateTimeOffset StartedAt, DateTimeOffset CrashAt, decimal CrashPoint, GrowthCurve Curve)
{
    public bool Ended { get; init; }
}
