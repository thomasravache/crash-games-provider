namespace Apogeu.Fairness.Contracts;

/// <summary>
/// Public contract v1 of the Fairness module (SPEC-0007 §6, ADR-0006): the seed chain of each room, the commitment of the
/// next round, the reveal of a played round and the pure crash-point calculation and verification.
/// </summary>
public interface IFairnessService
{
    /// <summary>Creates a new seed chain of <paramref name="length"/> links for the room; it becomes the room's active chain.</summary>
    Task<ChainInfo> CreateChainAsync(SalaId sala, int length, CancellationToken ct);

    /// <summary>Issues the next round of the room's active chain. Throws <see cref="ChainExhaustedException"/> after round N.</summary>
    Task<RoundSeedCommitment> NextRoundAsync(SalaId sala, CancellationToken ct);

    /// <summary>Reveals the seed of an issued round with its crash point. Throws <see cref="RoundNotFoundException"/>.</summary>
    Task<RevealedRound> RevealAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct);

    /// <summary>Pure: the crash point of algorithm v1 for the seed, salt, RTP and cap.</summary>
    CrashPoint ComputeCrashPoint(Seed seed, Salt salt, Rtp rtp, Multiplier cap);

    /// <summary>Pure: checks that SHA256(seed) = previousSeed and that the claimed crash point is the computed one.</summary>
    VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed);
}
