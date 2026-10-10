using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;

namespace Apogeu.Operators.Infrastructure.Catalog;

/// <summary>The bet limits of <paramref name="Operator"/> in <c>Limits.Currency</c>.</summary>
public sealed record OperatorCurrencyLimits(OperatorId Operator, BetLimits Limits);
