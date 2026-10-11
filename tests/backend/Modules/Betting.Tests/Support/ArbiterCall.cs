using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Tests.Support;

/// <summary>One decision run by an instance's <see cref="ICashOutArbiter"/>.</summary>
internal sealed record ArbiterCall(bool Leading, RoundId RoundId, DateTimeOffset ReceivedAt, CashOutDecision? Decision);
