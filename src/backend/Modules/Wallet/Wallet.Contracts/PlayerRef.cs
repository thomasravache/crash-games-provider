using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Wallet.Contracts;

/// <summary>A player of an operator (SPEC-0021 §6): the wallet keeps one account per operator, player and currency.</summary>
/// <param name="OperatorId">Operator the player came from.</param>
/// <param name="PlayerId">Pseudonymous player id sent by the operator (ADR-0014: no personal data).</param>
public sealed record PlayerRef(OperatorId OperatorId, PlayerId PlayerId);
