namespace Apogeu.Fairness.Contracts;

/// <summary>The round issued by the chain: its index r and the commitment SHA256(s_r), published before the bets.</summary>
public sealed record RoundSeedCommitment(ChainId ChainId, int RoundIndex, Seed Commitment);
