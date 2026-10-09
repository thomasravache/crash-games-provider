namespace Apogeu.Operators.Infrastructure.Persistence;

/// <summary>
/// A row of <c>operators.operator_limits</c>: the bet limits of an operator in one currency (SPEC-0020 §2); a currency
/// without a row is not enabled for the operator.
/// </summary>
internal sealed class OperatorLimitsRecord
{
    public required string OperatorId { get; set; }

    /// <summary>ISO 4217 code of the currency of the amounts.</summary>
    public required string Currency { get; set; }

    public decimal MinBet { get; set; }

    public decimal MaxBet { get; set; }

    public decimal MaxWin { get; set; }
}
