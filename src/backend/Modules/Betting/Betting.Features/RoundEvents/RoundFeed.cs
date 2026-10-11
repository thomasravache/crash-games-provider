using System.Collections.Concurrent;
using Apogeu.Betting.Domain;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Features.RoundEvents;

/// <summary>
/// <see cref="IRoundFeed"/> of this instance: the latest round received per room and, for <see cref="CurrentAsync"/>, the
/// room's round read through <see cref="ISalaPhase"/> (the same on every instance, emenda 3).
/// </summary>
internal sealed class RoundFeed(ISalaPhase phases) : IRoundFeed
{
    private readonly ConcurrentDictionary<string, RoundWindow> _rounds = new(StringComparer.Ordinal);

    public void Apply(RoundBettingOpened opened)
    {
        ArgumentNullException.ThrowIfNull(opened);
        _rounds[opened.SalaId] = new RoundWindow(opened.RoundId, opened.RoundNumber, opened.BettingEndsAt, RoundPhase.Betting);
    }

    public void Apply(RoundFlying flying)
    {
        ArgumentNullException.ThrowIfNull(flying);
        Move(flying.SalaId, flying.RoundId, RoundPhase.Flying, flying.StartedAt);
    }

    public void Apply(RoundCrashed crashed)
    {
        ArgumentNullException.ThrowIfNull(crashed);
        Move(crashed.SalaId, crashed.RoundId, RoundPhase.Ended, crashed.CrashedAt);
    }

    public void Apply(RoundAnnulled annulled)
    {
        ArgumentNullException.ThrowIfNull(annulled);
        Move(annulled.SalaId, annulled.RoundId, RoundPhase.Ended, at: null);
    }

    public RoundWindow? Current(string salaId) => _rounds.TryGetValue(salaId, out var round) ? round : null;

    public async Task<RoundWindow?> CurrentAsync(string salaId, CancellationToken ct)
    {
        var round = await phases.CurrentAsync(salaId, ct).ConfigureAwait(false);
        return round is null
            ? null
            : new RoundWindow(round.RoundId, round.RoundNumber, round.BettingEndsAt, round.State switch
            {
                RoundState.Betting => RoundPhase.Betting,
                RoundState.Flying => RoundPhase.Flying,
                _ => RoundPhase.Ended,
            });
    }

    /// <summary>
    /// The round moved to <paramref name="phase"/>. A round whose opening this instance did not receive (it started after
    /// the round opened) is still known from then on, with its bets closed.
    /// </summary>
    private void Move(string salaId, RoundId roundId, RoundPhase phase, DateTimeOffset? at) =>
        _rounds.AddOrUpdate(
            salaId,
            _ => new RoundWindow(roundId, RoundNumber: 0, at ?? DateTimeOffset.MinValue, phase),
            (_, current) => current.RoundId == roundId
                ? current with { Phase = phase }
                : new RoundWindow(roundId, RoundNumber: 0, at ?? DateTimeOffset.MinValue, phase));
}
