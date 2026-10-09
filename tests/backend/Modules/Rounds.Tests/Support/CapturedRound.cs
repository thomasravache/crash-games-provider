using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// A round followed end to end by a real SignalR client connected to the instance that does not lead the room (the messages
/// arrive through the Redis backplane), with the snapshot of <c>JoinSala</c> and the answer of <c>GetServerTime</c>.
/// </summary>
internal sealed record CapturedRound(
    RoundsHost Follower,
    HubConnection Connection,
    HubRecorder Recorder,
    Guid RoundId,
    JsonElement Snapshot,
    JsonElement ServerTime) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
