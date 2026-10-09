using Apogeu.Fairness.Contracts;

namespace Apogeu.Fairness.Domain;

/// <summary>Public verification of a round (SPEC-0007 §6): chaining SHA256(seed) = previousSeed, then the crash point.</summary>
public static class FairnessVerifier
{
    public static VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(previousSeed);
        ArgumentNullException.ThrowIfNull(salt);

        if (SeedChain.Next(seed) != previousSeed)
        {
            return VerificationResult.ChainMismatch;
        }

        return CrashPointCalculator.Compute(seed, salt, rtp, cap) == claimed
            ? VerificationResult.Valid
            : VerificationResult.CrashPointMismatch;
    }
}
