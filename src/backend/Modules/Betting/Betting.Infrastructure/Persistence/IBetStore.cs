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

    /// <summary>The placed and queued bets and the pending reservations of <paramref name="owner"/> (the ones that hold a panel).</summary>
    Task<IReadOnlyList<Bet>> ListOpenAsync(BetOwner owner, CancellationToken ct);

    /// <summary>
    /// Writes <paramref name="bet"/> as a pending reservation (committed at once), which holds its panel. Throws
    /// <see cref="BetConflictException"/> when the player already has a bet or reservation with its <c>clientBetId</c>, or
    /// in its panel.
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

    /// <summary>
    /// Stores, through the Wolverine outbox, a new reversal of the debit of <paramref name="bet"/> (same deterministic
    /// <c>bet:{betId}:rollback</c>): a debit the wallet applied after its reservation was voided (IT-12).
    /// </summary>
    Task ReverseLateDebitAsync(Bet bet, CancellationToken ct);

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

    /// <summary>The accepted bet <paramref name="betId"/> of any player (the handlers of SPEC-0023); none for a pending reservation.</summary>
    Task<AcceptedBet?> FindByIdAsync(Guid betId, CancellationToken ct);

    /// <summary>The active bets of <paramref name="roundId"/> with an automatic cash-out (scheduled with the leader at the flight).</summary>
    Task<IReadOnlyList<Bet>> ListAutoCashOutsAsync(RoundId roundId, CancellationToken ct);

    /// <summary>
    /// Moves the active <paramref name="bet"/>, now cashed out, to <c>cashed_out</c> with its multiplier and payout and its credit
    /// pending, storing in the same transaction the durable credit of the payout scheduled after <paramref name="creditDelay"/>
    /// (Wolverine outbox, ADR-0007); with <paramref name="announce"/> that credit also tells the player the cash-out. Returns
    /// whether this call cashed it out (it was still active).
    /// </summary>
    Task<bool> CashOutAsync(Bet bet, bool automatic, DateTimeOffset at, TimeSpan creditDelay, bool announce, CancellationToken ct);

    /// <summary>
    /// Records the reception instant of a manual cash-out the leader did not decide in time — the first one stays: a later
    /// instant never replaces it, an earlier concurrent one does — with the single durable command that has it decided later
    /// (stored with the first record). Returns whether the bet is active with a pending request.
    /// </summary>
    Task<bool> RequestCashOutAsync(Guid betId, DateTimeOffset receivedAt, CancellationToken ct);

    /// <summary>Forgets the pending request of <paramref name="betId"/>, refused by the leader (received after the crash).</summary>
    Task ClearCashOutRequestAsync(Guid betId, CancellationToken ct);

    /// <summary>The wallet confirmed the credit of the cash-out of <paramref name="betId"/>; whether this call recorded it (it was pending).</summary>
    Task<bool> ConfirmCreditAsync(Guid betId, decimal balance, CancellationToken ct);
}
