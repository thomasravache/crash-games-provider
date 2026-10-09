using Apogeu.BuildingBlocks.Contracts;
namespace Apogeu.Operators.Contracts;

/// <summary>The operator has no limits configured in the currency (<see cref="OperatorsErrorCodes.CurrencyNotEnabled"/>).</summary>
public sealed class CurrencyNotEnabledException : Exception
{
    public CurrencyNotEnabledException()
        : base("The currency is not enabled for the operator.")
    {
    }

    public CurrencyNotEnabledException(string message)
        : base(message)
    {
    }

    public CurrencyNotEnabledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public CurrencyNotEnabledException(OperatorId op, Currency currency)
        : base($"The currency '{currency.Code}' is not enabled for the operator '{op.Value}'.")
    {
        Operator = op;
        Currency = currency;
    }

    /// <summary>The contract error code, <c>CURRENCY_NOT_ENABLED</c>.</summary>
    public string Code { get; } = OperatorsErrorCodes.CurrencyNotEnabled;

    public OperatorId Operator { get; }

    public Currency Currency { get; }
}
