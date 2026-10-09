using System.Diagnostics.Metrics;
using Apogeu.Fairness.Contracts;

namespace Apogeu.Fairness.Infrastructure;

/// <summary>Business metrics of the Fairness module (SPEC-0007 §8, ADR-0017). Tags carry ids only, never seeds.</summary>
internal sealed class FairnessMetrics
{
    /// <summary>Name of the meter, to be added to the OpenTelemetry metrics of the host.</summary>
    public const string MeterName = "Apogeu.Fairness";

    private const string SalaTag = "fairness.sala_id";

    private readonly Counter<long> _roundsIssued;
    private readonly Gauge<long> _chainRemaining;

    public FairnessMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _roundsIssued = meter.CreateCounter<long>(
            "fairness.rounds_issued", unit: "{round}", description: "Rounds issued from the seed chains.");
        _chainRemaining = meter.CreateGauge<long>(
            "fairness.chain_remaining", unit: "{round}", description: "Rounds left in the active seed chain of the room.");
    }

    public void ChainCreated(SalaId sala, int length) =>
        _chainRemaining.Record(length, new KeyValuePair<string, object?>(SalaTag, sala.Value));

    public void RoundIssued(SalaId sala, int remaining)
    {
        var tag = new KeyValuePair<string, object?>(SalaTag, sala.Value);
        _roundsIssued.Add(1, tag);
        _chainRemaining.Record(remaining, tag);
    }
}
