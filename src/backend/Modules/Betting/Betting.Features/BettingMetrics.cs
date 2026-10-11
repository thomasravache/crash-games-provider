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

    public const string CashOuts = "betting.cashouts";

    public const string CashOutTooLate = "betting.cashout_too_late";

    public const string CashOutDecisionLatency = "betting.cashout_decision_latency";

    public const string CreditsPending = "betting.credits_pending";

    /// <summary>Activities of the module (a trace per <c>betId</c>, SPEC-0022 §8).</summary>
    internal static readonly ActivitySource Activities = new(MeterName);

    private readonly Counter<long> _bets;
    private readonly Counter<long> _rejected;
    private readonly Histogram<long> _acceptLatency;
    private readonly Counter<long> _cashOuts;
    private readonly Counter<long> _cashOutTooLate;
    private readonly Histogram<long> _decisionLatency;
    private readonly UpDownCounter<long> _creditsPending;

    public BettingMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);
        _bets = meter.CreateCounter<long>(Bets, unit: "{bet}", description: "Bets by the status they reached (placed, queued, active, cancelled).");
        _rejected = meter.CreateCounter<long>(Rejected, unit: "{bet}", description: "Bets and cancellations refused, by error code.");
        _acceptLatency = meter.CreateHistogram<long>(AcceptLatency, unit: "us", description: "From receiving PlaceBet to its accepted answer.");
        _cashOuts = meter.CreateCounter<long>(CashOuts, unit: "{bet}", description: "Bets cashed out, manual or automatic (SPEC-0023).");
        _cashOutTooLate = meter.CreateCounter<long>(CashOutTooLate, unit: "{bet}", description: "Manual cash-outs received at or after the crash.");
        _decisionLatency = meter.CreateHistogram<long>(CashOutDecisionLatency, unit: "us", description: "From receiving CashOut to the leader's decision.");
        _creditsPending = meter.CreateUpDownCounter<long>(CreditsPending, unit: "{credit}", description: "Credits of cash-outs not confirmed by the wallet yet.");
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

    public void CashedOut(bool automatic) => _cashOuts.Add(1, new KeyValuePair<string, object?>("betting.auto", automatic));

    public void TooLate() => _cashOutTooLate.Add(1);

    public void Decided(TimeSpan latency) => _decisionLatency.Record(latency.Ticks / TimeSpan.TicksPerMicrosecond);

    /// <summary>A credit left pending (<paramref name="delta"/> = 1) or confirmed after being pending (-1).</summary>
    public void CreditPending(int delta) => _creditsPending.Add(delta);
}
