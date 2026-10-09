namespace Apogeu.Rounds.Domain;

/// <summary>
/// The public data of a Fairness seed chain a room plays (SPEC-0007 <c>ChainInfo</c>): recorded by Rounds when it creates
/// the chain, so the verification of any of its rounds can show the terminal hash and the salt.
/// </summary>
/// <param name="ChainId">The Fairness chain id.</param>
/// <param name="SalaId">Id of the room.</param>
/// <param name="TerminalHash">Lowercase hex of h_N, published when the chain was created.</param>
/// <param name="Salt">Lowercase hex of the public salt of the chain.</param>
/// <param name="Length">Number of rounds N of the chain.</param>
/// <param name="CreatedAt">When the chain was created.</param>
public sealed record RoundChain(Guid ChainId, string SalaId, string TerminalHash, string Salt, int Length, DateTimeOffset CreatedAt);
