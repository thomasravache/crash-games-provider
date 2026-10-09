using System.Diagnostics.Metrics;

namespace Apogeu.Operators.Features.Observability;

/// <summary>Security metrics of SPEC-0009 §8. Tags carry contract codes and endpoint names only (ADR-0014, ADR-0017).</summary>
internal sealed class OperatorsMetrics
{
    /// <summary>Name of the meter, added to the OpenTelemetry metrics of the host.</summary>
    public const string MeterName = "Apogeu.Operators";

    private readonly Counter<long> _launchRejected;
    private readonly Counter<long> _replayDetected;
    private readonly Counter<long> _rateLimited;

    public OperatorsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);
        _launchRejected = meter.CreateCounter<long>(
            "security.launch_rejected", unit: "{request}", description: "Launch requests rejected, by contract error code.");
        _replayDetected = meter.CreateCounter<long>(
            "security.replay_detected", unit: "{request}", description: "Signed operator requests rejected as replays.");
        _rateLimited = meter.CreateCounter<long>(
            "security.rate_limited", unit: "{request}", description: "Requests rejected by a rate limit, by endpoint.");
    }

    public void LaunchRejected(string reason) => _launchRejected.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void ReplayDetected() => _replayDetected.Add(1);

    public void RateLimited(string endpoint) => _rateLimited.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint));
}
