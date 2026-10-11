using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.CashOut;

/// <summary>An automatic cash-out the leader executes at <paramref name="At"/> = <c>startedAt + ln(target)/k</c>.</summary>
internal sealed record ScheduledAutoCashOut(RoundId RoundId, Guid BetId, Multiplier Target, DateTimeOffset At);
