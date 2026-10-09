#pragma warning disable MA0025 // Scaffold of SPEC-0008 (no logic yet): implemented in the green phase.

using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.Live;
using Microsoft.Extensions.Logging;

namespace Apogeu.Rounds.Features.Conductor;

/// <summary>
/// Conducts the rounds of one room while this instance leads it (SPEC-0008 §4): Betting → Flying → Crashed → new Betting,
/// on the server clock (<see cref="TimeProvider"/>). At the start of the flight it gets the crash point with
/// <c>GetCrashPointAsync</c> (SPEC-0007@2) and keeps it in memory only; <c>RevealAsync</c> is called only after the crash.
/// On its first step it resumes the latest round of the store (same <c>startedAt</c>; beyond 30 s without a leader →
/// Annulled).
/// </summary>
public sealed class SalaConductor
{
    /// <summary>Interval of <c>RoundSync</c> during the flight; syncs fall on <c>startedAt + n·SyncInterval</c>.</summary>
    public static readonly TimeSpan SyncInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>Deadline to resume an interrupted round before it is annulled (ADR-0018).</summary>
    public static readonly TimeSpan ResumeDeadline = TimeSpan.FromSeconds(30);

    public SalaConductor(
        SalaConfig sala,
        IFairnessService fairness,
        IRoundStore store,
        IRoundBroadcaster broadcaster,
        RoundsMetrics metrics,
        TimeProvider time,
        ILogger<SalaConductor> logger)
    {
        ArgumentNullException.ThrowIfNull(sala);
        ArgumentNullException.ThrowIfNull(fairness);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(broadcaster);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
    }

    /// <summary>
    /// Runs every action due at the current time of the <see cref="TimeProvider"/> (open bets, start the flight, sync,
    /// crash, annul) and returns the instant of the next action. The leader loop waits until that instant and calls it again.
    /// </summary>
    public Task<DateTimeOffset> StepAsync(CancellationToken ct) => throw new NotImplementedException();
}
