using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Apogeu.Rounds.Features.Conductor;

namespace Apogeu.Rounds.Features.Live;

/// <summary>
/// The live state of each room this instance leads, published by its conductor (IMP-02). A view is served only while the
/// leadership term that published it holds its lease and the conductor that published it runs: a follower, a leader that
/// lost its lease or a conductor that failed has no view, and its joins read the store instead.
/// </summary>
internal sealed class SalaLiveViews
{
    private readonly ConcurrentDictionary<string, LiveView> _views = new(StringComparer.Ordinal);

    /// <summary>A publisher for the conductor of <paramref name="salaId"/> in the term of <paramref name="lease"/>; disposing it withdraws its view.</summary>
    public Publisher Open(string salaId, LeaderLease lease) => new(this, salaId, lease);

    /// <summary>The live state of the room, when this instance leads it and its conductor already published.</summary>
    public bool TryGet(string salaId, [NotNullWhen(true)] out LiveState? state)
    {
        if (_views.TryGetValue(salaId, out var view) && view.Publisher.IsValid)
        {
            state = view.State;
            return true;
        }

        state = null;
        return false;
    }

    /// <summary>The state of a room: its current round (null before the first) and its last crashed rounds, newest first.</summary>
    internal sealed record LiveState(CurrentRound? Current, IReadOnlyList<RoundSummary> History);

    private sealed record LiveView(Publisher Publisher, LiveState State);

    internal sealed class Publisher(SalaLiveViews views, string salaId, LeaderLease lease) : ISalaSnapshotPublisher, IDisposable
    {
        private volatile bool _disposed;

        public bool IsValid => !_disposed && lease.IsValid;

        public void Publish(CurrentRound? current, IReadOnlyList<RoundSummary> history)
        {
            ArgumentNullException.ThrowIfNull(history);
            if (_disposed)
            {
                return;
            }

            views._views[salaId] = new LiveView(this, new LiveState(current, [.. history]));
        }

        public void Dispose()
        {
            // A view published concurrently with the disposal stays behind but is never served: it is no longer valid.
            _disposed = true;
            if (views._views.TryGetValue(salaId, out var view) && ReferenceEquals(view.Publisher, this))
            {
                views._views.TryRemove(new KeyValuePair<string, LiveView>(salaId, view));
            }
        }
    }
}
