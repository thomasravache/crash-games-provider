namespace Apogeu.Fairness.Contracts;

/// <summary>A revealed round: the seed s_r, the previous seed s_{r-1}, the chain salt and the crash point.</summary>
public sealed record RevealedRound(Seed Seed, Seed PreviousSeed, Salt Salt, CrashPoint CrashPoint);
