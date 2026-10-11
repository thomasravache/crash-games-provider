using Apogeu.Betting.Domain;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>
/// Persistence of the bets (schema <c>betting</c>, ADR-0010). A bet is first written as a pending reservation, before its
/// debit, and confirmed after it (SPEC-0022 emenda 3): no transaction stays open across the call to the wallet, and a
/// reservation never confirmed is voided with its debit reversed (<see cref="VoidAsync"/>, by the bet or by the sweeper).
/// </summary>
public interface IBetStore
{
    /// <summary>The bet (or pending reservation) <paramref name="owner"/> made with <paramref name="clientBetId"/>.</summary>
    Task<AcceptedBet?> FindByClientBetIdAsync(BetOwner owner, Guid clientBetId, CancellationToken ct);

    /// <summary>The accepted bet <paramref name="betId"/> of <paramref name="owner"/>; <see langword="null"/> for a bet of anyone else.</summary>
    Task<AcceptedBet?> FindAsync(BetOwner owner, Guid betId, CancellationToken ct);

    /// <summary>The placed and queued bets of <paramref name="owner"/> (the ones that hold a panel).</summary>
    Task<IReadOnlyList<Bet>> ListOpenAsync(BetOwner owner, CancellationToken ct);

    /// <summary>
    /// Writes <paramref name="bet"/> as a pending reservation (committed at once). Throws <see cref="BetConflictException"/>
    /// when the player already has a bet or reservation with its <c>clientBetId</c>.
    /// </summary>
    Task ReserveAsync(Bet bet, string salaId, DateTimeOffset now, CancellationToken ct);

    /// <summary>The debit of the reservation <paramref name="betId"/> was confirmed: it becomes the accepted bet.</summary>
    Task<BetConfirmation> ConfirmAsync(Guid betId, decimal balanceAfter, CancellationToken ct);

    /// <summary>Deletes the pending reservation <paramref name="betId"/> whose debit was not applied (nothing to reverse).</summary>
    Task ReleaseAsync(Guid betId, CancellationToken ct);

    /// <summary>
    /// Deletes the pending reservation <paramref name="betId"/> and stores the reversal of its debit in the same transaction
    /// (Wolverine outbox). Returns whether this call voided it (it was still pending).
    /// </summary>
    Task<bool> VoidAsync(Guid betId, CancellationToken ct);

    /// <summary>The pending reservations created before <paramref name="createdBefore"/> (at most <paramref name="limit"/>).</summary>
    Task<IReadOnlyList<Guid>> ListStalePendingAsync(DateTimeOffset createdBefore, int limit, CancellationToken ct);

    /// <summary>
    /// Moves <paramref name="bet"/> to its current status if it is still in <paramref name="expected"/>; whether it moved. A
    /// bet moved to cancelled has the reversal of its debit stored in the same transaction (Wolverine outbox).
    /// </summary>
    Task<bool> TransitionAsync(Bet bet, BetStatus expected, CancellationToken ct);

    /// <summary>As <see cref="TransitionAsync(Bet, BetStatus, CancellationToken)"/>, also recording the refund a cancellation answered.</summary>
    Task<bool> TransitionAsync(Bet bet, BetStatus expected, BetRefund? refund, CancellationToken ct);

    /// <summary>The flight of <paramref name="roundId"/> started: its placed bets become active. Returns how many.</summary>
    Task<int> StartFlightAsync(RoundId roundId, CancellationToken ct);

    /// <summary>
    /// The room <paramref name="salaId"/> opened the bets of <paramref name="opened"/>: its queued bets (or only
    /// <paramref name="betId"/>) become placed in it, each on its own (one failure does not stop the others). Returns the
    /// bets this call moved.
    /// </summary>
    Task<IReadOnlyList<Bet>> ActivateQueuedAsync(string salaId, RoundWindow opened, Guid? betId, CancellationToken ct);
}
