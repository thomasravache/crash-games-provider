using System.Diagnostics.Metrics;

namespace Apogeu.Rounds.Features;

/// <summary>
/// Business metrics of the Rounds module (SPEC-0008 §8), on a meter created by the host's <see cref="IMeterFactory"/>:
/// <c>rounds.started</c>, <c>rounds.crashed</c>, <c>rounds.annulled</c>, <c>rounds.leader_changes</c> and
/// <c>rounds.crash_broadcast_latency</c>.
/// </summary>
public sealed class RoundsMetrics
{
    public const string MeterName = "Apogeu.Rounds";

    public const string Started = "rounds.started";

    public const string Crashed = "rounds.crashed";

    public const string Annulled = "rounds.annulled";

    public const string LeaderChanges = "rounds.leader_changes";

    public const string CrashBroadcastLatency = "rounds.crash_broadcast_latency";

    public RoundsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
    }
}
