using Apogeu.Rounds.Features.Live;

namespace Apogeu.Rounds.Features.Conductor;

/// <summary>
/// The lease of one leadership term (fencing): valid until <see cref="End"/>, extended by each renewal that answers in
/// time. The conductor checks it before each step and each broadcast, so a leader paused past its lease (a stalled
/// process, a hung connection) never acts once another instance may hold the lock; an expired check ends the term.
/// </summary>
internal sealed class LeaderLease(DateTimeOffset end, TimeProvider time, CancellationTokenSource term)
{
    private long _endTicks = end.UtcTicks;

    /// <summary>When this instance must stop leading.</summary>
    public DateTimeOffset End => new(Interlocked.Read(ref _endTicks), TimeSpan.Zero);

    /// <summary>Whether the lease still holds and the term goes on.</summary>
    public bool IsValid => !term.IsCancellationRequested && time.GetUtcNow() < End;

    /// <summary>Extends the lease after a renewal that answered in time.</summary>
    public void Extend(DateTimeOffset end) => Interlocked.Exchange(ref _endTicks, end.UtcTicks);

    /// <summary>
    /// Throws <see cref="OperationCanceledException"/> on the term's token, ending the term, when the lease no longer holds.
    /// </summary>
    public void Ensure()
    {
        if (IsValid)
        {
            return;
        }

        term.Cancel();
        term.Token.ThrowIfCancellationRequested();
    }

    /// <summary>The room broadcaster of this term: every message is sent only while the lease holds.</summary>
    public IRoundBroadcaster Fence(IRoundBroadcaster broadcaster) => new FencedBroadcaster(this, broadcaster);

    private sealed class FencedBroadcaster(LeaderLease lease, IRoundBroadcaster inner) : IRoundBroadcaster
    {
        public IRoundsClient Sala(string salaId) => new FencedClient(lease, inner.Sala(salaId));
    }

    private sealed class FencedClient(LeaderLease lease, IRoundsClient inner) : IRoundsClient
    {
        public Task RoundBetting(RoundBettingMessage message)
        {
            lease.Ensure();
            return inner.RoundBetting(message);
        }

        public Task RoundFlying(RoundFlyingMessage message)
        {
            lease.Ensure();
            return inner.RoundFlying(message);
        }

        public Task RoundSync(RoundSyncMessage message)
        {
            lease.Ensure();
            return inner.RoundSync(message);
        }

        public Task RoundCrashed(RoundCrashedMessage message)
        {
            lease.Ensure();
            return inner.RoundCrashed(message);
        }

        public Task RoundAnnulled(RoundAnnulledMessage message)
        {
            lease.Ensure();
            return inner.RoundAnnulled(message);
        }
    }
}
