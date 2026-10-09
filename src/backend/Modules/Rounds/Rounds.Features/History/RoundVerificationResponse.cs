using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Rounds.Features.History;

/// <summary>Answer of <c>GET /api/v1/rounds/{roundId}/verification</c>: the data SPEC-0007's <c>Verify</c> accepts.</summary>
public sealed record RoundVerificationResponse(
    string Seed,
    string PreviousSeed,
    string Salt,
    string Rtp,
    Multiplier Cap,
    Multiplier CrashPoint,
    string TerminalHash);
