namespace Apogeu.Fairness.Contracts;

/// <summary>Outcome of <see cref="IFairnessService.Verify"/>.</summary>
public enum VerificationResult
{
    /// <summary>The seed chains with the previous seed and the claimed crash point is the computed one.</summary>
    Valid,

    /// <summary>SHA256(seed) differs from the previous seed.</summary>
    ChainMismatch,

    /// <summary>The seed chains, but the claimed crash point differs from the computed one.</summary>
    CrashPointMismatch,
}
