namespace Apogeu.Rounds.Features.Live;

/// <summary>
/// Where the leader's conductor publishes the live state of its room after each transition it persisted and before it
/// broadcasts it (IMP-02): <c>JoinSala</c> on the leader is answered from this state, with no database read.
/// </summary>
public interface ISalaSnapshotPublisher
{
    /// <summary>
    /// The current round (crash point and seed only once it crashed, as persisted) and the last crashed rounds, newest first.
    /// </summary>
    void Publish(CurrentRound? current, IReadOnlyList<RoundSummary> history);
}
