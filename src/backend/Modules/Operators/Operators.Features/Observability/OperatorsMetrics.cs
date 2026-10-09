using System.Diagnostics.Metrics;

namespace Apogeu.Operators.Features.Observability;

/// <summary>
/// Security metrics of SPEC-0009 §8 and the catalog rejections of SPEC-0020 §8. Tags carry contract codes and endpoint names
/// only (ADR-0014, ADR-0017).
/// </summary>
internal sealed class OperatorsMetrics
{
    /// <summary>Name of the meter, added to the OpenTelemetry metrics of the host.</summary>
    public const string MeterName = "Apogeu.Operators";

    private readonly Counter<long> _launchRejected;
    private readonly Counter<long> _replayDetected;
    private readonly Counter<long> _rateLimited;
    private readonly Counter<long> _catalogLaunchRejected;

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
        _catalogLaunchRejected = meter.CreateCounter<long>(
            "operators.launch_rejected",
            unit: "{request}",
            description: "Launch requests rejected by the game catalog or the operator limits, by contract error code.");
    }

    public void LaunchRejected(string reason) => _launchRejected.Add(1, new KeyValuePair<string, object?>("reason", reason));

    /// <summary>A launch refused by the catalog or the limits (<c>GAME_NOT_AVAILABLE</c>, <c>CURRENCY_NOT_ENABLED</c>).</summary>
    public void CatalogLaunchRejected(string reason) =>
        _catalogLaunchRejected.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void ReplayDetected() => _replayDetected.Add(1);

    public void RateLimited(string endpoint) => _rateLimited.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint));
}
