using Apogeu.Betting.Domain;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Features.RoundEvents;

/// <summary>
/// The rounds of each room as Betting sees them (SPEC-0022 §6). The module's handlers of the Rounds integration events
/// (<see cref="RoundBettingOpened"/>, <see cref="RoundFlying"/>, <see cref="RoundCrashed"/>, <see cref="RoundAnnulled"/>,
/// delivered by Wolverine with the outbox, ADR-0002) apply every event they receive here, in the order received, before
/// acting on the bets. <c>PlaceBet</c> and <c>CancelBet</c> read <see cref="CurrentAsync"/> (<c>ISalaPhase</c>), the same on
/// every instance (emenda 3); <see cref="Current"/> is only what this instance received, for diagnostics.
/// </summary>
public interface IRoundFeed
{
    void Apply(RoundBettingOpened opened);

    void Apply(RoundFlying flying);

    void Apply(RoundCrashed crashed);

    void Apply(RoundAnnulled annulled);

    /// <summary>
    /// The latest round this instance received for <paramref name="salaId"/>, or <see langword="null"/> before the first event.
    /// Diagnostics only (and the tests' wait on the events received): the production phase source is <see cref="CurrentAsync"/>.
    /// </summary>
    RoundWindow? Current(string salaId);

    /// <summary>
    /// The current round of <paramref name="salaId"/> whichever instance conducts the room (<c>ISalaPhase</c>, emenda 3): the
    /// phase source of <c>PlaceBet</c> and <c>CancelBet</c>.
    /// </summary>
    Task<RoundWindow?> CurrentAsync(string salaId, CancellationToken ct);
}
