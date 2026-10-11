using Apogeu.Betting.Domain;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Features.RoundEvents;

/// <summary>
/// The rounds of each room as Betting sees them (SPEC-0022 §6). The module's handlers of the Rounds integration events
/// (<see cref="RoundBettingOpened"/>, <see cref="RoundFlying"/>, <see cref="RoundCrashed"/>, <see cref="RoundAnnulled"/>,
/// delivered by Wolverine with the outbox, ADR-0002) apply every event they receive here, in the order received, before
/// acting on the bets. <c>PlaceBet</c> and <c>CancelBet</c> read <see cref="CurrentAsync"/>, which is the same on every
/// instance (emenda 3); <see cref="Current"/> is what this instance received.
/// </summary>
public interface IRoundFeed
{
    void Apply(RoundBettingOpened opened);

    void Apply(RoundFlying flying);

    void Apply(RoundCrashed crashed);

    void Apply(RoundAnnulled annulled);

    /// <summary>The latest round this instance received for <paramref name="salaId"/>, or <see langword="null"/> before the first event.</summary>
    RoundWindow? Current(string salaId);

    /// <summary>
    /// The current round of <paramref name="salaId"/> whichever instance conducts the room. By default, what this instance
    /// received (<see cref="Current"/>).
    /// </summary>
    Task<RoundWindow?> CurrentAsync(string salaId, CancellationToken ct) => Task.FromResult(Current(salaId));
}
