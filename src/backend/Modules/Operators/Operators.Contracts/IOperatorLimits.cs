using System.Diagnostics.CodeAnalysis;
using Apogeu.BuildingBlocks.Contracts;
namespace Apogeu.Operators.Contracts;

/// <summary>Bet limits per operator and currency (SPEC-0020 §6, contract v1), read on every bet.</summary>
public interface IOperatorLimits
{
    /// <summary>
    /// The limits of <paramref name="op"/> in <paramref name="currency"/>; throws <see cref="CurrencyNotEnabledException"/>
    /// (<c>CURRENCY_NOT_ENABLED</c>) when the operator has no limits configured in that currency.
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Name fixed by the contract v1 of SPEC-0020 §6.")]
    BetLimits Get(OperatorId op, Currency currency);
}
