using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Apogeu.Wallet.Infrastructure.Observability;

/// <summary>
/// Metrics and traces of the Wallet module (SPEC-0021 §8), on a meter created by the host's <see cref="IMeterFactory"/>:
/// <c>wallet.operations{type,result}</c>, <c>wallet.duplicates</c> and <c>wallet.demo_recharges</c>, plus one activity per
/// operation tagged with its transaction id. Tags never carry amounts, balances or player ids (ADR-0014, ADR-0017).
/// </summary>
public sealed class WalletMetrics
{
    /// <summary>Name of the meter and of the activity source, added to the OpenTelemetry pipeline of the host.</summary>
    public const string MeterName = "Apogeu.Wallet";

    public const string Operations = "wallet.operations";

    public const string Duplicates = "wallet.duplicates";

    public const string DemoRecharges = "wallet.demo_recharges";

    /// <summary>Activities of the wallet operations, tagged with <c>wallet.transaction_id</c>.</summary>
    public static readonly ActivitySource ActivitySource = new(MeterName);

    private const string TypeTag = "type";
    private const string ResultTag = "result";

    private readonly Counter<long> _operations;
    private readonly Counter<long> _duplicates;
    private readonly Counter<long> _demoRecharges;

    public WalletMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(MeterName);
        _operations = meter.CreateCounter<long>(
            Operations, unit: "{operation}", description: "Wallet operations, by type (debit, credit, rollback) and result.");
        _duplicates = meter.CreateCounter<long>(
            Duplicates, unit: "{operation}", description: "Wallet operations answered with the result of an earlier one (same transaction id).");
        _demoRecharges = meter.CreateCounter<long>(
            DemoRecharges, unit: "{recharge}", description: "Demo balances recharged back to the opening amount.");
    }

    /// <summary>An operation of <paramref name="type"/> finished with <paramref name="result"/> (a duplicate also counts in <c>wallet.duplicates</c>).</summary>
    public void Operation(string type, string result)
    {
        _operations.Add(1, new KeyValuePair<string, object?>(TypeTag, type), new KeyValuePair<string, object?>(ResultTag, result));
        if (string.Equals(result, WalletResultNames.Duplicate, StringComparison.Ordinal))
        {
            _duplicates.Add(1, new KeyValuePair<string, object?>(TypeTag, type));
        }
    }

    public void DemoRecharged() => _demoRecharges.Add(1);
}
