using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Tests.Support;

/// <summary>A <see cref="IRoundFeed"/> that always answers the same round of every room (unit tests).</summary>
internal sealed class FixedRoundFeed(RoundWindow? current) : IRoundFeed
{
    public void Apply(RoundBettingOpened opened)
    {
    }

    public void Apply(RoundFlying flying)
    {
    }

    public void Apply(RoundCrashed crashed)
    {
    }

    public void Apply(RoundAnnulled annulled)
    {
    }

    public RoundWindow? Current(string salaId) => current;

    public Task<RoundWindow?> CurrentAsync(string salaId, CancellationToken ct) => Task.FromResult(current);
}
