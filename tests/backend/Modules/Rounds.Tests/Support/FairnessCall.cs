namespace Apogeu.Rounds.Tests.Support;

/// <summary>A call the conductor made to a Fairness double, with the fake server time of the call.</summary>
internal sealed record FairnessCall(string Method, int RoundIndex, DateTimeOffset At);
