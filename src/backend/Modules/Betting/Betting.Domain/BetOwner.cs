using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Domain;

/// <summary>Who a bet belongs to, from the player session (SPEC-0009): pseudonymous ids only (ADR-0014).</summary>
/// <param name="OperatorId">Operator the player came from.</param>
/// <param name="PlayerId">Pseudonymous player id.</param>
/// <param name="Currency">Currency of the session.</param>
public sealed record BetOwner(OperatorId OperatorId, PlayerId PlayerId, Currency Currency);
