#pragma warning disable MA0025 // Scaffold of SPEC-0008 (no logic yet): implemented in the green phase.

using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Domain;

/// <summary>
/// Aggregate of a round (SPEC-0008 §2): <c>Betting → Flying → Crashed</c>, or <c>Annulled</c> from Betting or Flying. Any
/// other transition throws <see cref="InvalidRoundTransitionException"/>. The crash point and the seed exist on the
/// aggregate only after the crash (SPEC-0008 §4, emenda v2).
/// </summary>
public sealed class Round
{
    private Round() => throw new NotImplementedException();

    public RoundId Id => throw new NotImplementedException();

    /// <summary>Id of the room (<c>^[a-z0-9-]{1,32}$</c>).</summary>
    public string SalaId => throw new NotImplementedException();

    /// <summary>Sequential number of the round in the room, from 1.</summary>
    public long Number => throw new NotImplementedException();

    /// <summary>The Fairness chain of the round (SPEC-0007 <c>ChainId</c>).</summary>
    public Guid ChainId => throw new NotImplementedException();

    /// <summary>The round index in the chain (SPEC-0007 <c>RoundIndex</c>).</summary>
    public int ChainRoundIndex => throw new NotImplementedException();

    /// <summary>Lowercase hex of SHA256(s_r), published before the bets.</summary>
    public string Commitment => throw new NotImplementedException();

    public RoundState State => throw new NotImplementedException();

    public DateTimeOffset BettingEndsAt => throw new NotImplementedException();

    public DateTimeOffset? StartedAt => throw new NotImplementedException();

    public DateTimeOffset? CrashedAt => throw new NotImplementedException();

    /// <summary>Set only by <see cref="Crash"/>.</summary>
    public decimal? CrashPoint => throw new NotImplementedException();

    /// <summary>Lowercase hex of the revealed seed; set only by <see cref="Crash"/>.</summary>
    public string? RevealedSeed => throw new NotImplementedException();

    public string? AnnulReason => throw new NotImplementedException();

    /// <summary>A new round in <see cref="RoundState.Betting"/>.</summary>
    public static Round OpenBetting(
        RoundId id,
        string salaId,
        long number,
        Guid chainId,
        int chainRoundIndex,
        string commitment,
        DateTimeOffset bettingEndsAt) => throw new NotImplementedException();

    /// <summary>Rebuilds a persisted round (stores only).</summary>
    public static Round Restore(
        RoundId id,
        string salaId,
        long number,
        Guid chainId,
        int chainRoundIndex,
        string commitment,
        RoundState state,
        DateTimeOffset bettingEndsAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? crashedAt,
        decimal? crashPoint,
        string? revealedSeed,
        string? annulReason) => throw new NotImplementedException();

    /// <summary>Betting → Flying.</summary>
    public void StartFlight(DateTimeOffset startedAt) => throw new NotImplementedException();

    /// <summary>Flying → Crashed, with the crash point and the revealed seed.</summary>
    public void Crash(decimal crashPoint, string revealedSeed, DateTimeOffset crashedAt) => throw new NotImplementedException();

    /// <summary>Betting or Flying → Annulled.</summary>
    public void Annul(string reason, DateTimeOffset annulledAt) => throw new NotImplementedException();
}
