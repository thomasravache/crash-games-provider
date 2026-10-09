using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;

namespace Apogeu.Operators.Infrastructure.Catalog;

/// <summary>The game <paramref name="Game"/> is enabled for the operator <paramref name="Operator"/>.</summary>
public sealed record GameEnablement(OperatorId Operator, GameCode Game);
