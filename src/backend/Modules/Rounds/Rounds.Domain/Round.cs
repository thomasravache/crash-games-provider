using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Domain;

/// <summary>
/// Aggregate of a round (SPEC-0008 §2): <c>Betting → Flying → Crashed</c>, or <c>Annulled</c> from Betting or Flying. Any
/// other transition throws <see cref="InvalidRoundTransitionException"/>. The crash point and the seed exist on the
/// aggregate only after the crash (SPEC-0008 §4, emenda v2).
/// </summary>
public sealed class Round
{
    private Round(
        RoundId id,
        string salaId,
        long number,
        Guid chainId,
        int chainRoundIndex,
        string commitment,
        RoundState state,
        DateTimeOffset bettingEndsAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(salaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitment);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(chainRoundIndex, 1);

        Id = id;
        SalaId = salaId;
        Number = number;
        ChainId = chainId;
        ChainRoundIndex = chainRoundIndex;
        Commitment = commitment;
        State = state;
        BettingEndsAt = bettingEndsAt;
    }

    public RoundId Id { get; }

    /// <summary>Id of the room (<c>^[a-z0-9-]{1,32}$</c>).</summary>
    public string SalaId { get; }

    /// <summary>Sequential number of the round in the room, from 1.</summary>
    public long Number { get; }

    /// <summary>The Fairness chain of the round (SPEC-0007 <c>ChainId</c>).</summary>
    public Guid ChainId { get; }

    /// <summary>The round index in the chain (SPEC-0007 <c>RoundIndex</c>).</summary>
    public int ChainRoundIndex { get; }

    /// <summary>Lowercase hex of SHA256(s_r), published before the bets.</summary>
    public string Commitment { get; }

    public RoundState State { get; private set; }

    public DateTimeOffset BettingEndsAt { get; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CrashedAt { get; private set; }

    /// <summary>Set only by <see cref="Crash"/>.</summary>
    public decimal? CrashPoint { get; private set; }

    /// <summary>Lowercase hex of the revealed seed; set only by <see cref="Crash"/>.</summary>
    public string? RevealedSeed { get; private set; }

    public string? AnnulReason { get; private set; }

    /// <summary>When the round was annulled; set only by <see cref="Annul"/>.</summary>
    public DateTimeOffset? AnnulledAt { get; private set; }

    /// <summary>
    /// The parameters the round is played with (RTP and cap of its crash point, growth constant of its flight), fixed when
    /// its bets open and kept for its resumption and verification; <see langword="null"/> only for a round opened without
    /// them, to which the room's current configuration applies. Never the crash point itself.
    /// </summary>
    public RoundParameters? Parameters { get; private init; }

    /// <summary>Whether the round reached a terminal state (Crashed or Annulled).</summary>
    public bool IsFinished => State is RoundState.Crashed or RoundState.Annulled;

    /// <summary>A new round in <see cref="RoundState.Betting"/>.</summary>
    public static Round OpenBetting(
        RoundId id,
        string salaId,
        long number,
        Guid chainId,
        int chainRoundIndex,
        string commitment,
        DateTimeOffset bettingEndsAt,
        RoundParameters? parameters = null) =>
        new(id, salaId, number, chainId, chainRoundIndex, commitment, RoundState.Betting, bettingEndsAt) { Parameters = parameters };

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
        string? annulReason,
        DateTimeOffset? annulledAt = null,
        RoundParameters? parameters = null) =>
        new(id, salaId, number, chainId, chainRoundIndex, commitment, state, bettingEndsAt)
        {
            Parameters = parameters,
            StartedAt = startedAt,
            CrashedAt = state == RoundState.Crashed ? crashedAt : null,
            CrashPoint = state == RoundState.Crashed ? crashPoint : null,
            RevealedSeed = state == RoundState.Crashed ? revealedSeed : null,
            AnnulReason = state == RoundState.Annulled ? annulReason : null,
            AnnulledAt = state == RoundState.Annulled ? annulledAt : null,
        };

    /// <summary>Betting → Flying.</summary>
    public void StartFlight(DateTimeOffset startedAt)
    {
        EnsureState(RoundState.Betting, nameof(StartFlight));
        StartedAt = startedAt;
        State = RoundState.Flying;
    }

    /// <summary>Flying → Crashed, with the crash point and the revealed seed.</summary>
    public void Crash(decimal crashPoint, string revealedSeed, DateTimeOffset crashedAt)
    {
        EnsureState(RoundState.Flying, nameof(Crash));
        ArgumentOutOfRangeException.ThrowIfLessThan(crashPoint, 1.00m);
        ArgumentException.ThrowIfNullOrWhiteSpace(revealedSeed);
        if (crashedAt < StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(crashedAt), crashedAt, "A round cannot crash before its flight started.");
        }

        CrashPoint = crashPoint;
        RevealedSeed = revealedSeed;
        CrashedAt = crashedAt;
        State = RoundState.Crashed;
    }

    /// <summary>Betting or Flying → Annulled.</summary>
    public void Annul(string reason, DateTimeOffset annulledAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (IsFinished)
        {
            throw new InvalidRoundTransitionException($"Round {Number} of room '{SalaId}' is {State}; it cannot be annulled.");
        }

        AnnulReason = reason;
        AnnulledAt = annulledAt;
        State = RoundState.Annulled;
    }

    private void EnsureState(RoundState expected, string transition)
    {
        if (State != expected)
        {
            throw new InvalidRoundTransitionException(
                $"Round {Number} of room '{SalaId}' is {State}; {transition} requires {expected}.");
        }
    }
}
