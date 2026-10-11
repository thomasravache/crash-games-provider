using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary><c>BalanceUpdated</c> (SPEC-0023 §6): a credit retried later was confirmed by the wallet.</summary>
/// <param name="Balance">Balance after the credit.</param>
/// <param name="Currency">ISO 4217 currency of the session.</param>
public sealed record BalanceUpdatedMessage(Money Balance, string Currency);
