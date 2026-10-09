using Apogeu.Fairness.Contracts;

namespace Apogeu.Fairness.Domain;

/// <summary>What is kept of a new chain: its checkpoints (secret, h_0 included) and its public terminal hash h_N.</summary>
public sealed record ChainLayout(IReadOnlyList<ChainCheckpoint> Checkpoints, Seed TerminalHash);
