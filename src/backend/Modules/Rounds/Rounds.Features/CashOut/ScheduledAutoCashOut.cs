using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.CashOut;

/// <summary>
/// An automatic cash-out the leader executes at <paramref name="At"/> = <c>startedAt + ln(target)/k</c>; one whose publication
/// failed is tried again at <paramref name="RetryAt"/>, still executed at <paramref name="At"/>.
/// </summary>
internal sealed record ScheduledAutoCashOut(RoundId RoundId, Guid BetId, Multiplier Target, DateTimeOffset At, DateTimeOffset? RetryAt = null)
{
    /// <summary>When the leader publishes it.</summary>
    public DateTimeOffset DueAt => RetryAt ?? At;
}
