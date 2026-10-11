using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Apogeu.Betting.Features;

/// <summary>
/// Business metrics of the Betting module (SPEC-0022 §8), on a meter created by the host's <see cref="IMeterFactory"/>:
/// <c>betting.bets{status}</c>, <c>betting.rejected{code}</c> and <c>betting.accept_latency</c>. Tags never carry a player.
/// </summary>
public sealed class BettingMetrics
{
    public const string MeterName = "Apogeu.Betting";

    public const string Bets = "betting.bets";

    public const string Rejected = "betting.rejected";

    public const string AcceptLatency = "betting.accept_latency";

    /// <summary>Activities of the module (a trace per <c>betId</c>, SPEC-0022 §8).</summary>
    internal static readonly ActivitySource Activities = new(MeterName);

    private readonly Counter<long> _bets;
    private readonly Counter<long> _rejected;
    private readonly Histogram<long> _acceptLatency;

    public BettingMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);
        _bets = meter.CreateCounter<long>(Bets, unit: "{bet}", description: "Bets by the status they reached (placed, queued, active, cancelled).");
        _rejected = meter.CreateCounter<long>(Rejected, unit: "{bet}", description: "Bets and cancellations refused, by error code.");
        _acceptLatency = meter.CreateHistogram<long>(AcceptLatency, unit: "us", description: "From receiving PlaceBet to its accepted answer.");
    }

    public void BetReached(string status, long count = 1)
    {
        if (count > 0)
        {
            _bets.Add(count, new KeyValuePair<string, object?>("betting.status", status));
        }
    }

    public void RejectedWith(string code) => _rejected.Add(1, new KeyValuePair<string, object?>("betting.code", code));

    public void Accepted(TimeSpan latency) => _acceptLatency.Record(latency.Ticks / TimeSpan.TicksPerMicrosecond);
}
