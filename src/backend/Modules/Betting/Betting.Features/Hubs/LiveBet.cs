using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>A bet of the current round as the room sees it (SPEC-0025 §6): no bet id and no full nickname.</summary>
/// <param name="BetRef">Opaque public reference of the bet (never the <c>betId</c>).</param>
/// <param name="Nickname">Anonymized nickname, as <c>"Ma***"</c>.</param>
/// <param name="Amount">Amount of the bet.</param>
/// <param name="Currency">ISO 4217 currency of the bet.</param>
/// <param name="Multiplier">Multiplier of the cash-out, once cashed out.</param>
/// <param name="Payout">Payout of the cash-out, once cashed out.</param>
public sealed record LiveBet(
    string BetRef,
    string Nickname,
    Money Amount,
    string Currency,
    Multiplier? Multiplier,
    Money? Payout);
