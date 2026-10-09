namespace Apogeu.Fairness.Contracts;

/// <summary>Public data of a chain, published at creation: the terminal hash h_N, the salt and the length N.</summary>
public sealed record ChainInfo(ChainId ChainId, Seed TerminalHash, Salt Salt, int Length);
