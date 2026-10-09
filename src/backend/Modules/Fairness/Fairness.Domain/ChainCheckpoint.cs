using Apogeu.Fairness.Contracts;

namespace Apogeu.Fairness.Domain;

/// <summary>The link h_<paramref name="LinkIndex"/> of a chain, stored so that any link is a few hashes away.</summary>
public sealed record ChainCheckpoint(int LinkIndex, Seed Link);
