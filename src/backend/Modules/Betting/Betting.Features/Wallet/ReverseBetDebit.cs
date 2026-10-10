namespace Apogeu.Betting.Features.Wallet;

/// <summary>
/// Durable command (Wolverine local queue, ADR-0002) to reverse the debit of a bet with <c>RollbackAsync</c> until the wallet
/// confirms it (ADR-0007): after an uncertain debit, or a cancellation whose refund the wallet did not confirm.
/// </summary>
/// <param name="BetId">The bet whose debit <c>bet:{betId}:debit</c> is reversed by <c>bet:{betId}:rollback</c>.</param>
/// <param name="OperatorId">Operator of the player.</param>
/// <param name="PlayerId">Pseudonymous player id.</param>
/// <param name="Currency">Currency of the account.</param>
/// <param name="RoundId">Round of the bet; <see langword="null"/> for a bet queued for the next round (SPEC-0021@4).</param>
public sealed record ReverseBetDebit(Guid BetId, string OperatorId, string PlayerId, string Currency, Guid? RoundId);
