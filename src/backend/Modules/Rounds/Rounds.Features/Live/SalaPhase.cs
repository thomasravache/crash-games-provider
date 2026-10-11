using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary><see cref="ISalaPhase"/> over the room snapshot of <c>JoinSala</c> (<see cref="SalaSnapshotQuery"/>, IMP-02).</summary>
internal sealed class SalaPhase(SalaSnapshotQuery snapshots) : ISalaPhase
{
    public async Task<SalaRound?> CurrentAsync(string salaId, CancellationToken ct)
    {
        var snapshot = await snapshots.GetAsync(salaId, ct).ConfigureAwait(false);
        return snapshot?.Current is { } current
            ? new SalaRound(new RoundId(current.RoundId), current.RoundNumber, current.State, current.BettingEndsAt)
            : null;
    }
}
