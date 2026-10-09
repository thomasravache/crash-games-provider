namespace Apogeu.Rounds.Tests.Support;

/// <summary>One message the conductor sent to a room group, with the fake server time at which it was sent.</summary>
internal sealed record BroadcastEvent(string SalaId, string Target, object Message, DateTimeOffset SentAt);
