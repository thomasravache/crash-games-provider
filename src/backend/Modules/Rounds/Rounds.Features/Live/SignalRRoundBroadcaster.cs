using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Rounds.Features.Live;

/// <summary><see cref="IRoundBroadcaster"/> over the room groups of <see cref="RoundsHub"/> (Redis backplane, ADR-0005).</summary>
internal sealed class SignalRRoundBroadcaster(IHubContext<RoundsHub, IRoundsClient> hub) : IRoundBroadcaster
{
    public IRoundsClient Sala(string salaId) => hub.Clients.Group(RoundsHub.GroupOf(salaId));
}
