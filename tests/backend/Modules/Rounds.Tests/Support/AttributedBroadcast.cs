namespace Apogeu.Rounds.Tests.Support;

/// <summary>One message an instance's conductor sent (wall clock at the call), attributed to that instance.</summary>
internal sealed record AttributedBroadcast(DateTimeOffset SentAt, string Target, Guid RoundId)
{
    public override string ToString() => $"{SentAt:O} {Target} {RoundId}";
}
