using Apogeu.Fairness.Contracts;

namespace Apogeu.Fairness.Domain;

#pragma warning disable MA0025 // Scaffold of SPEC-0007 (no logic yet): implemented in the green phase.

/// <summary>Public verification of a round (SPEC-0007 §6): chaining SHA256(seed) = previousSeed, then the crash point.</summary>
public static class FairnessVerifier
{
    public static VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed) =>
        throw new NotImplementedException();
}
#pragma warning restore MA0025
