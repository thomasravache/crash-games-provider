using Apogeu.Betting.Features.Hubs;

namespace Apogeu.Betting.Features.LiveBets;

/// <summary>
/// Read projection of the live bets of each room (SPEC-0025 §4, ADR-0019): records the bets and cash-outs of the current
/// round and groups them in windows of <see cref="Window"/>; each batch carries at most <see cref="MaxItems"/> bets (the
/// highest amounts) and <see cref="MaxItems"/> cash-outs, the rest only counting in the totals. Times come from the
/// injected <see cref="TimeProvider"/>.
/// </summary>
#pragma warning disable CS9113 // Scaffold of SPEC-0025 (no logic yet): the clock is read in the green phase.
public sealed class LiveBetsBatcher(TimeProvider time)
#pragma warning restore CS9113
{
    /// <summary>Window of a batch: at most one batch per room every 200 ms.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(200);

    /// <summary>Most bets (and most cash-outs) of a batch or of a snapshot.</summary>
    public const int MaxItems = 100;

    /// <summary>Records a bet accepted in its round; a new round of the room restarts its totals.</summary>
    public void Record(LiveBetPlaced placed) =>
#pragma warning disable MA0025 // Scaffold of SPEC-0025 (no logic yet): implemented in the green phase.
        throw new NotImplementedException();
#pragma warning restore MA0025

    /// <summary>Records a cash-out of a bet recorded before.</summary>
    public void Record(LiveBetCashedOut cashedOut) =>
#pragma warning disable MA0025 // Scaffold of SPEC-0025 (no logic yet): implemented in the green phase.
        throw new NotImplementedException();
#pragma warning restore MA0025

    /// <summary>The batches of the rooms whose window elapsed with news, removing that news from the pending window.</summary>
    public IReadOnlyList<SalaLiveBetsBatch> TakeDue() =>
#pragma warning disable MA0025 // Scaffold of SPEC-0025 (no logic yet): implemented in the green phase.
        throw new NotImplementedException();
#pragma warning restore MA0025

    /// <summary>The snapshot of the current round of <paramref name="salaId"/>; <see langword="null"/> when none was recorded.</summary>
    public LiveBetsSnapshot? SnapshotOf(string salaId) =>
#pragma warning disable MA0025 // Scaffold of SPEC-0025 (no logic yet): implemented in the green phase.
        throw new NotImplementedException();
#pragma warning restore MA0025
}
