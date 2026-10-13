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

    /// <summary>
    /// The accepted bets of the player of <paramref name="owner"/> at its operator (no pending reservation), by acceptance
    /// instant descending and then by id descending, strictly after <paramref name="before"/> when given; at most
    /// <paramref name="limit"/> (SPEC-0025 §6).
    /// </summary>
    Task<IReadOnlyList<PlayerBet>> ListPlayerBetsAsync(BetOwner owner, PlayerBetsCursor? before, int limit, CancellationToken ct);

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
    /// <remarks>
    /// The hub records it before asking the leader (SPEC-0024 review G4, M1), so the crash never marks lost a bet whose cash-out
    /// is being decided; the command is delayed by <paramref name="decisionDelay"/> as a fallback of the hub's own decision.
    /// </remarks>
    Task<bool> RequestCashOutAsync(Guid betId, DateTimeOffset receivedAt, TimeSpan decisionDelay, CancellationToken ct);

    /// <summary>Stores a <c>DecideCashOut</c> of <paramref name="betId"/> to run at once (the leader did not decide in time).</summary>
    Task ScheduleDecisionAsync(Guid betId, CancellationToken ct);

    /// <summary>
    /// Forgets the pending request of <paramref name="betId"/>, refused by the leader (received after the crash). Returns whether
    /// this call forgot it (one refusal is told once).
    /// </summary>
    Task<bool> ClearCashOutRequestAsync(Guid betId, CancellationToken ct);

    /// <summary>
    /// A cash-out the leader accepted for a bet already marked lost (SPEC-0024 review G4, M1): <c>lost → cashed_out</c> (compare-and-set)
    /// with its durable credit, only while the round's summary is not closed. Returns whether this call cashed it out.
    /// </summary>
    Task<bool> ReviveCashOutAsync(Bet bet, DateTimeOffset at, TimeSpan creditDelay, CancellationToken ct);

    /// <summary>The wallet confirmed the credit of the cash-out of <paramref name="betId"/>; whether this call recorded it (it was pending).</summary>
    Task<bool> ConfirmCreditAsync(Guid betId, decimal balance, CancellationToken ct);

    /// <summary>How Betting processed the end of <paramref name="roundId"/>: <c>crashed</c>, <c>annulled</c>, or <see langword="null"/> not yet.</summary>
    Task<string?> RoundOutcomeAsync(RoundId roundId, CancellationToken ct);

    /// <summary>Records that Betting processed the end of <paramref name="roundId"/> (<c>crashed</c> or <c>annulled</c>); idempotent.</summary>
    Task MarkRoundEndedAsync(RoundId roundId, string outcome, DateTimeOffset at, CancellationToken ct);

    /// <summary>
    /// The round crashed (SPEC-0024): its active bets without a pending cash-out request become lost, in one statement. Returns
    /// the bets this call made lost (a repeated crash makes none).
    /// </summary>
    Task<IReadOnlyList<Bet>> SettleLostAsync(RoundId roundId, CancellationToken ct);

    /// <summary>
    /// The round was annulled (SPEC-0024): its placed and active bets (pending cash-out requests included) become refunded with
    /// their full amount, each with its durable refund stored in the same transaction (Wolverine outbox, ADR-0007). Returns the
    /// bets this call refunded.
    /// </summary>
    /// <remarks>With <paramref name="placedOnly"/>, only the placed bets: a crashed round whose activation of them was lost (IT-07).</remarks>
    Task<IReadOnlyList<Bet>> RefundRoundAsync(RoundId roundId, bool placedOnly, CancellationToken ct);

    /// <summary>The wallet confirmed the refund of <paramref name="betId"/>; whether this call recorded it (it was pending).</summary>
    /// <remarks><paramref name="refunded"/> is what the refund returned: 0,00 when the wallet had no debit to reverse.</remarks>
    Task<bool> ConfirmRefundAsync(Guid betId, decimal refunded, decimal balance, CancellationToken ct);

    /// <summary>
    /// Closes the summary of <paramref name="roundId"/> when its end was processed and no bet of it is open (placed, active):
    /// one immutable row per operator and currency, written once (unique index, <c>ON CONFLICT DO NOTHING</c>). Returns whether
    /// this call wrote it.
    /// </summary>
    /// <remarks>Not before <paramref name="grace"/> after the round's end was processed: a cash-out in flight may still revive a lost bet.</remarks>
    Task<bool> CloseSettlementAsync(RoundId roundId, DateTimeOffset at, TimeSpan grace, CancellationToken ct);
}
