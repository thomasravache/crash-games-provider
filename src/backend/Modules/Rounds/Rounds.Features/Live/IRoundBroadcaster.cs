namespace Apogeu.Rounds.Features.Live;

/// <summary>Sends the round messages to the clients of a room group (SignalR with the Redis backplane, ADR-0005).</summary>
public interface IRoundBroadcaster
{
    IRoundsClient Sala(string salaId);
}
