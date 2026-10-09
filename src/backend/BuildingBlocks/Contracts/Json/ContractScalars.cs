namespace Apogeu.BuildingBlocks.Contracts.Json;

/// <summary>
/// Wire description of the contract scalars, whose custom converters hide their shape from schema exporters
/// (ADR-0008): every one is a JSON string with a <c>format</c>. <c>money</c> and <c>multiplier</c> become the branded
/// <c>Money</c> and <c>Multiplier</c> types in TypeScript (tools/contracts).
/// </summary>
public static class ContractScalars
{
    public const string MoneyFormat = "money";

    public const string MultiplierFormat = "multiplier";

    public const string DateTimeFormat = "date-time";

    /// <summary>At least two decimal places, as written by <see cref="MoneyJsonConverter"/> and <see cref="MultiplierJsonConverter"/>.</summary>
    public const string DecimalStringPattern = @"^-?\d+\.\d{2,}$";

    /// <summary>
    /// String schema of <paramref name="type"/> when it is a contract scalar (or a <see cref="Nullable{T}"/> of one);
    /// <see langword="null"/> otherwise.
    /// </summary>
    public static ContractScalar? Describe(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var underlying = Nullable.GetUnderlyingType(type);
        var scalar = underlying ?? type;
        var nullable = underlying is not null;
        return scalar switch
        {
            _ when scalar == typeof(Money) => new ContractScalar(MoneyFormat, DecimalStringPattern, nullable),
            _ when scalar == typeof(Multiplier) => new ContractScalar(MultiplierFormat, DecimalStringPattern, nullable),
            _ when scalar == typeof(DateTimeOffset) => new ContractScalar(DateTimeFormat, Pattern: null, nullable),
            _ => null,
        };
    }
}
