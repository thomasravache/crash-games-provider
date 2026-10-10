using Apogeu.Betting.Domain;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>Persistence of the bets (schema <c>betting</c>, ADR-0010).</summary>
public interface IBetStore
{
    /// <summary>The bet <paramref name="owner"/> placed with <paramref name="clientBetId"/>, with the result it was accepted with.</summary>
    Task<AcceptedBet?> FindByClientBetIdAsync(BetOwner owner, Guid clientBetId, CancellationToken ct);

    /// <summary>The bet <paramref name="betId"/> of <paramref name="owner"/>; <see langword="null"/> for a bet of anyone else.</summary>
    Task<AcceptedBet?> FindAsync(BetOwner owner, Guid betId, CancellationToken ct);

    /// <summary>The placed and queued bets of <paramref name="owner"/> (the ones that can hold a panel).</summary>
    Task<IReadOnlyList<Bet>> ListOpenAsync(BetOwner owner, CancellationToken ct);

    /// <summary>
    /// Starts the acceptance of <paramref name="bet"/>: inserts it in a transaction that stays open until
    /// <see cref="IBetReservation.CommitAsync"/> (a concurrent bet with the same <c>clientBetId</c> waits for it); disposing the
    /// reservation without committing leaves nothing. Throws <see cref="BetConflictException"/> when the <c>clientBetId</c> or
    /// the panel is already taken.
    /// </summary>
    Task<IBetReservation> ReserveAsync(Bet bet, string salaId, DateTimeOffset now, CancellationToken ct);

    /// <summary>Moves <paramref name="bet"/> to its current status if it is still in <paramref name="expected"/>; whether it moved.</summary>
    Task<bool> TransitionAsync(Bet bet, BetStatus expected, CancellationToken ct);

    /// <summary>The flight of <paramref name="roundId"/> started: its placed bets become active. Returns how many.</summary>
    Task<int> StartFlightAsync(RoundId roundId, CancellationToken ct);

    /// <summary>
    /// The room <paramref name="salaId"/> opened the bets of <paramref name="opened"/>: its queued bets (or only
    /// <paramref name="betId"/>) become placed in it. Returns the bets this call moved.
    /// </summary>
    Task<IReadOnlyList<Bet>> ActivateQueuedAsync(string salaId, RoundWindow opened, Guid? betId, CancellationToken ct);
}
