using Apogeu.Fairness.Contracts;

namespace Apogeu.Fairness.Domain;

#pragma warning disable MA0025 // Scaffold of SPEC-0007 (no logic yet): implemented in the green phase.

/// <summary>The hash chain of algorithm v1 (SPEC-0007 §4): h_{k+1} = SHA256(h_k) over the raw 32 bytes.</summary>
public static class SeedChain
{
    /// <summary>SHA256 of the seed: the next link h_{k+1}, and also the commitment of the round that uses the seed.</summary>
    public static Seed Next(Seed seed) => throw new NotImplementedException();
}
#pragma warning restore MA0025
