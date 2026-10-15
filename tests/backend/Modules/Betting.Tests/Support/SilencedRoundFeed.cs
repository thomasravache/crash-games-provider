using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Decorator of the module's <see cref="IRoundFeed"/> (SPEC-0025 IT-05): forwards everything to the real feed until
/// <see cref="Silence"/>, after which <see cref="CurrentAsync"/> answers that the room has no current round, so a room whose
/// previous round had bets is seen without a current round.
/// </summary>
internal sealed class SilencedRoundFeed(IRoundFeed inner) : IRoundFeed
{
    private int _silenced;

    /// <summary>From now on, no room has a current round.</summary>
    public void Silence() => Volatile.Write(ref _silenced, 1);

    public void Apply(RoundBettingOpened opened) => inner.Apply(opened);

    public void Apply(RoundFlying flying) => inner.Apply(flying);

    public void Apply(RoundCrashed crashed) => inner.Apply(crashed);

    public void Apply(RoundAnnulled annulled) => inner.Apply(annulled);

    public RoundWindow? Current(string salaId) => inner.Current(salaId);

    /// <summary>Forwarded (the production phase source) until <see cref="Silence"/>; then <see langword="null"/>.</summary>
    public Task<RoundWindow?> CurrentAsync(string salaId, CancellationToken ct) =>
        Volatile.Read(ref _silenced) == 1 ? Task.FromResult<RoundWindow?>(null) : inner.CurrentAsync(salaId, ct);
}
