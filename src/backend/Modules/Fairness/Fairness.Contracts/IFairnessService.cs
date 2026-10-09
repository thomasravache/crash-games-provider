namespace Apogeu.Fairness.Contracts;

/// <summary>
/// Public contract v2 of the Fairness module (SPEC-0007 §6, ADR-0006): the seed chain of each room, the commitment of the
/// next round, the crash point of an issued round without its seed, the reveal of a played round and the pure crash-point calculation and verification.
/// </summary>
public interface IFairnessService
{
    /// <summary>Creates a new seed chain of <paramref name="length"/> links for the room; it becomes the room's active chain.</summary>
    Task<ChainInfo> CreateChainAsync(SalaId sala, int length, CancellationToken ct);

    /// <summary>Issues the next round of the room's active chain. Throws <see cref="ChainExhaustedException"/> after round N.</summary>
    Task<RoundSeedCommitment> NextRoundAsync(SalaId sala, CancellationToken ct);

    /// <summary>Reveals the seed of an issued round with its crash point. Throws <see cref="RoundNotFoundException"/>.</summary>
    /// <remarks>
    /// Callers reveal a round only after its crash. The service rejects rounds not issued yet, but it does reveal the round
    /// currently issued (the one still in play), so the consumer that runs the round (SPEC-0008) calls this method only once
    /// the round has crashed; to schedule the crash before that it uses <see cref="GetCrashPointAsync"/>.
    /// </remarks>
    Task<RevealedRound> RevealAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct);

    /// <summary>
    /// (v2) The crash point of an issued round, without exposing its seed. Throws <see cref="RoundNotFoundException"/> for a
    /// round not issued yet.
    /// </summary>
    /// <remarks>
    /// Lets the consumer that runs the round (SPEC-0008) schedule the crash at the start of the flight while the seed stays
    /// inside the module (ADR-0014). Neither the seed nor the crash point is ever logged.
    /// </remarks>
    Task<CrashPoint> GetCrashPointAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct);

    /// <summary>Pure: the crash point of algorithm v1 for the seed, salt, RTP and cap.</summary>
    CrashPoint ComputeCrashPoint(Seed seed, Salt salt, Rtp rtp, Multiplier cap);

    /// <summary>Pure: checks that SHA256(seed) = previousSeed and that the claimed crash point is the computed one.</summary>
    VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed);
}
