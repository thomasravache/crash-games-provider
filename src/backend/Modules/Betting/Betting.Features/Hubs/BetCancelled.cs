using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>Result of <c>CancelBet</c> (SPEC-0022 §6): the bet is cancelled and its full amount refunded.</summary>
public sealed record BetCancelled(Guid BetId, Money Refunded, Money Balance);
