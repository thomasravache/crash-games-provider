using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>
/// The bet was cashed out (SPEC-0023 §6, contract v2): the result of <c>CashOut</c> (<c>auto:false</c>), and the server
/// message of an automatic cash-out (<c>auto:true</c>) or of the late decision of a <see cref="CashOutPending"/>. A new
/// <c>CashOut</c> of the same bet answers the same values.
/// </summary>
/// <param name="BetId">The bet.</param>
/// <param name="Multiplier">Multiplier of the cash-out (truncated in cents; the exact target when automatic).</param>
/// <param name="Payout">Payout: <c>min(floor(amount × multiplier × 100)/100, maxWin)</c>.</param>
/// <param name="CreditStatus"><c>credited</c> or <c>pending</c>.</param>
/// <param name="Balance">Balance after the credit; <see langword="null"/> while pending (<c>BalanceUpdated</c> follows).</param>
/// <param name="Auto">Whether the cash-out was the automatic one executed by the server.</param>
public sealed record CashedOut(
    Guid BetId,
    Multiplier Multiplier,
    Money Payout,
    CreditStatus CreditStatus,
    Money? Balance,
    bool Auto);
