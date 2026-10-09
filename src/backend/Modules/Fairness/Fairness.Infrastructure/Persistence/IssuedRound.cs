namespace Apogeu.Fairness.Infrastructure.Persistence;

/// <summary>Result of the atomic increment of <c>fairness.chains.issued_rounds</c>.</summary>
internal sealed record IssuedRound(Guid ChainId, int RoundIndex, int Length);
