using Apogeu.Rounds.Features.Live;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>Records every message the conductor sends, in order (unit tests: no SignalR).</summary>
internal sealed class RecordingBroadcaster(TimeProvider time) : IRoundBroadcaster
{
    private readonly Lock _gate = new();
    private readonly List<BroadcastEvent> _events = [];

    public IReadOnlyList<BroadcastEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public IReadOnlyList<T> Of<T>() => [.. Events.Select(e => e.Message).OfType<T>()];

    public IRoundsClient Sala(string salaId) => new Client(this, salaId);

    /// <summary>Every recorded message, one per line, for failure messages.</summary>
    public string Describe() => string.Join(Environment.NewLine, Events.Select(e => $"{e.SentAt:O} {e.Target} {e.Message}"));

    private void Record(string salaId, string target, object message)
    {
        lock (_gate)
        {
            _events.Add(new BroadcastEvent(salaId, target, message, time.GetUtcNow()));
        }
    }

    private sealed class Client(RecordingBroadcaster owner, string salaId) : IRoundsClient
    {
        public Task RoundBetting(RoundBettingMessage message) => Send(nameof(RoundBetting), message);

        public Task RoundFlying(RoundFlyingMessage message) => Send(nameof(RoundFlying), message);

        public Task RoundSync(RoundSyncMessage message) => Send(nameof(RoundSync), message);

        public Task RoundCrashed(RoundCrashedMessage message) => Send(nameof(RoundCrashed), message);

        public Task RoundAnnulled(RoundAnnulledMessage message) => Send(nameof(RoundAnnulled), message);

        private Task Send(string target, object message)
        {
            owner.Record(salaId, target, message);
            return Task.CompletedTask;
        }
    }
}
