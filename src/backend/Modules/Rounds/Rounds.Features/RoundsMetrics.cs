using System.Diagnostics.Metrics;

namespace Apogeu.Rounds.Features;

/// <summary>
/// Business metrics of the Rounds module (SPEC-0008 §8), on a meter created by the host's <see cref="IMeterFactory"/>:
/// <c>rounds.started</c>, <c>rounds.crashed</c>, <c>rounds.annulled</c>, <c>rounds.leader_changes</c> and
/// <c>rounds.crash_broadcast_latency</c>. Tags carry the room id only, never a crash point or a seed.
/// </summary>
public sealed class RoundsMetrics
{
    public const string MeterName = "Apogeu.Rounds";

    public const string Started = "rounds.started";

    public const string Crashed = "rounds.crashed";

    public const string Annulled = "rounds.annulled";

    public const string LeaderChanges = "rounds.leader_changes";

    public const string CrashBroadcastLatency = "rounds.crash_broadcast_latency";

    private const string SalaTag = "rounds.sala_id";

    private readonly Counter<long> _started;
    private readonly Counter<long> _crashed;
    private readonly Counter<long> _annulled;
    private readonly Counter<long> _leaderChanges;
    private readonly Histogram<double> _crashBroadcastLatency;

    public RoundsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);
        _started = meter.CreateCounter<long>(Started, unit: "{round}", description: "Rounds whose flight started.");
        _crashed = meter.CreateCounter<long>(Crashed, unit: "{round}", description: "Rounds that crashed.");
        _annulled = meter.CreateCounter<long>(
            Annulled, unit: "{round}", description: "Rounds annulled because they could not be resumed in time (alert).");
        _leaderChanges = meter.CreateCounter<long>(
            LeaderChanges, unit: "{change}", description: "Times this instance took the leadership of a room.");
        _crashBroadcastLatency = meter.CreateHistogram<double>(
            CrashBroadcastLatency, unit: "ms", description: "From the crash instant to the RoundCrashed message handed to the backplane.");
    }

    public void RoundStarted(string salaId) => _started.Add(1, Tag(salaId));

    public void RoundCrashed(string salaId, TimeSpan broadcastLatency)
    {
        _crashed.Add(1, Tag(salaId));
        _crashBroadcastLatency.Record(Math.Max(0, broadcastLatency.TotalMilliseconds), Tag(salaId));
    }

    public void RoundAnnulled(string salaId) => _annulled.Add(1, Tag(salaId));

    public void LeaderChanged(string salaId) => _leaderChanges.Add(1, Tag(salaId));

    private static KeyValuePair<string, object?> Tag(string salaId) => new(SalaTag, salaId);
}
