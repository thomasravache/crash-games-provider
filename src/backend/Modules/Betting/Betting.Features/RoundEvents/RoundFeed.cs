using Apogeu.Betting.Domain;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Features.RoundEvents;

#pragma warning disable MA0025 // Scaffold of SPEC-0022 (no logic yet): implemented in the green phase.

/// <summary>In-memory <see cref="IRoundFeed"/> of this instance.</summary>
internal sealed class RoundFeed : IRoundFeed
{
    public void Apply(RoundBettingOpened opened) => throw new NotImplementedException();

    public void Apply(RoundFlying flying) => throw new NotImplementedException();

    public void Apply(RoundCrashed crashed) => throw new NotImplementedException();

    public void Apply(RoundAnnulled annulled) => throw new NotImplementedException();

    public RoundWindow? Current(string salaId) => throw new NotImplementedException();
}
#pragma warning restore MA0025
