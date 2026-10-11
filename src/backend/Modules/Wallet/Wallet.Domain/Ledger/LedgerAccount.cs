using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;

namespace Apogeu.Wallet.Domain.Ledger;

/// <summary>
/// An account of the double-entry ledger (SPEC-0021 §2): the player's account, or the house account of the operator, in
/// one currency. <paramref name="Player"/> is <see langword="null"/> for the house.
/// </summary>
public sealed record LedgerAccount(OperatorId Operator, Currency Currency, PlayerId? Player)
{
    /// <summary>The account of <paramref name="player"/> in <paramref name="currency"/>.</summary>
    public static LedgerAccount OfPlayer(PlayerRef player, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(player);
        return new LedgerAccount(player.OperatorId, currency, player.PlayerId);
    }

    /// <summary>The house account of <paramref name="op"/> in <paramref name="currency"/>.</summary>
    public static LedgerAccount OfHouse(OperatorId op, Currency currency) => new(op, currency, Player: null);

    public bool IsHouse => Player is null;
}
