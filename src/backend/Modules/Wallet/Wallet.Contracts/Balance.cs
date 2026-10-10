using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Wallet.Contracts;

/// <summary>Balance of a player account in one currency (SPEC-0021 §6).</summary>
/// <param name="Amount">Available amount.</param>
/// <param name="Currency">Currency of the account.</param>
public sealed record Balance(Money Amount, Currency Currency);
