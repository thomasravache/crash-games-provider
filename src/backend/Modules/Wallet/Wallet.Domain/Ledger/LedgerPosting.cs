using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Wallet.Domain.Ledger;

/// <summary>One side of a ledger entry: a signed amount on one account (negative takes from it, positive adds to it).</summary>
public sealed record LedgerPosting(LedgerAccount Account, Money Amount);
