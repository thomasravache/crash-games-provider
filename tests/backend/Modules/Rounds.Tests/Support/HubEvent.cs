using System.Text.Json;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>A message received by a real SignalR client: the client method, its raw JSON argument and the receipt time.</summary>
internal sealed record HubEvent(string Target, JsonElement Payload, DateTimeOffset ReceivedAt)
{
    public Guid RoundId => Payload.GetProperty("roundId").GetGuid();

    public override string ToString() => $"{ReceivedAt:O} {Target} {Payload.GetRawText()}";
}
