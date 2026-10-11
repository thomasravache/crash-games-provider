using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>Result of <c>PlaceBet</c> (SPEC-0022 §6): the bet was debited and accepted.</summary>
/// <param name="BetId">Id of the bet.</param>
/// <param name="ClientBetId">Idempotency key sent by the client.</param>
/// <param name="RoundId">Round of the bet; <see langword="null"/> while queued.</param>
/// <param name="RoundNumber">Number of the round; <see langword="null"/> while queued.</param>
/// <param name="Panel">Panel 1 or 2.</param>
/// <param name="Amount">Amount debited.</param>
/// <param name="Currency">ISO 4217 currency of the session.</param>
/// <param name="AutoCashOut">Automatic cash-out, when set.</param>
/// <param name="Status"><c>placed</c> or <c>queued</c>.</param>
/// <param name="Balance">Balance right after the debit.</param>
public sealed record BetAccepted(
    Guid BetId,
    Guid ClientBetId,
    Guid? RoundId,
    long? RoundNumber,
    int Panel,
    Money Amount,
    string Currency,
    Multiplier? AutoCashOut,
    BetAcceptedStatus Status,
    Money Balance);
