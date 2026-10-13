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

    /// <summary>Calls to the operator wallet (seamless, SPEC-0046), by operation and answer.</summary>
    public const string RemoteCalls = "wallet.remote_calls";

    /// <summary>Resends of credits and rollbacks to the operator wallet (SPEC-0046).</summary>
    public const string Retries = "wallet.retries";

    /// <summary>Rollbacks confirmed by the operator whose debit was never mirrored here (SPEC-0046): to reconcile.</summary>
    public const string UnmirroredRollbacks = "wallet.unmirrored_rollbacks";

    /// <summary>Activities of the wallet operations, tagged with <c>wallet.transaction_id</c>.</summary>
    public static readonly ActivitySource ActivitySource = new(MeterName);

    private const string TypeTag = "type";
    private const string ResultTag = "result";
    private const string OperationTag = "op";
    private const string StatusTag = "status";

    private readonly Counter<long> _operations;
    private readonly Counter<long> _duplicates;
    private readonly Counter<long> _demoRecharges;
    private readonly Counter<long> _remoteCalls;
    private readonly Counter<long> _retries;
    private readonly Counter<long> _unmirroredRollbacks;

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
        _remoteCalls = meter.CreateCounter<long>(
            RemoteCalls, unit: "{call}", description: "Calls to the operator wallet (seamless), by operation and answer status.");
        _retries = meter.CreateCounter<long>(
            Retries, unit: "{call}", description: "Resends of credits and rollbacks to the operator wallet, by operation.");
        _unmirroredRollbacks = meter.CreateCounter<long>(
            UnmirroredRollbacks, unit: "{rollback}", description: "Rollbacks confirmed by the operator wallet whose debit was never mirrored in the ledger.");
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

    /// <summary>A call of <paramref name="operation"/> to the operator wallet answered <paramref name="status"/> (or <c>uncertain</c>).</summary>
    public void RemoteCall(string operation, string status) =>
        _remoteCalls.Add(1, new KeyValuePair<string, object?>(OperationTag, operation), new KeyValuePair<string, object?>(StatusTag, status));

    /// <summary>A pending <paramref name="operation"/> was resent to the operator wallet.</summary>
    public void Retried(string operation) => _retries.Add(1, new KeyValuePair<string, object?>(OperationTag, operation));

    /// <summary>A rollback was confirmed by the operator wallet but its debit was never mirrored here.</summary>
    public void RollbackUnmirrored() => _unmirroredRollbacks.Add(1);
}
