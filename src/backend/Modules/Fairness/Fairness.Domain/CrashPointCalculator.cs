using Apogeu.Fairness.Contracts;

namespace Apogeu.Fairness.Domain;

#pragma warning disable MA0025 // Scaffold of SPEC-0007 (no logic yet): implemented in the green phase.

/// <summary>
/// The crash point of algorithm v1 (SPEC-0007 §4): H = HMAC-SHA256(key = seed, message = salt bytes), X = the first 52 bits
/// of H, E = 2^52, crash = min(cap, max(1.00, floor(100 · RTP · E / (E − X)) / 100)) in exact integer arithmetic.
/// </summary>
public static class CrashPointCalculator
{
    /// <summary>The crash point of the seed and salt for the RTP and cap.</summary>
    public static CrashPoint Compute(Seed seed, Salt salt, Rtp rtp, Multiplier cap) => throw new NotImplementedException();

    /// <summary>The crash point of a sample X in [0, 2^52) for the RTP and cap (step 4 of the algorithm).</summary>
    public static CrashPoint FromSample(long sample, Rtp rtp, Multiplier cap) => throw new NotImplementedException();
}
#pragma warning restore MA0025
