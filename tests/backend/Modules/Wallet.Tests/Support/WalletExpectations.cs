using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Xunit;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>
/// The values SPEC-0021 §2 and §6 fix (demo operator, BRL, opening balance 3.000,00, minimum bet 1,00 of SPEC-0020) and
/// the deterministic transaction ids <c>bet:{betId}:debit|credit|rollback</c>, written here independently of the production code.
/// </summary>
internal static class WalletExpectations
{
    public static readonly OperatorId Demo = new("demo");
    public static readonly Currency Brl = new("BRL");

    /// <summary>Opening demo balance (§2), implicit: no ledger entry.</summary>
    public const decimal OpeningBalance = 3_000.00m;

    /// <summary>Minimum BRL bet of the demo operator (SPEC-0020 §2).</summary>
    public const decimal MinBet = 1.00m;

    public static PlayerRef NewPlayer() => new(Demo, new PlayerId("player-" + Guid.NewGuid().ToString("N")));

    public static TransactionId DebitId(Guid betId) => new("bet:" + betId.ToString("D") + ":debit");

    public static TransactionId CreditId(Guid betId) => new("bet:" + betId.ToString("D") + ":credit");

    public static TransactionId RollbackId(Guid betId) => new("bet:" + betId.ToString("D") + ":rollback");

    public static WalletOperation Debit(PlayerRef player, decimal amount, Guid betId, RoundId? round = null) =>
        new(DebitId(betId), player, Brl, new Money(amount), round ?? new RoundId(Guid.NewGuid()), betId);

    public static WalletOperation Credit(PlayerRef player, decimal amount, Guid betId, RoundId? round = null) =>
        new(CreditId(betId), player, Brl, new Money(amount), round ?? new RoundId(Guid.NewGuid()), betId);

    /// <summary>The rollback of the debit of <paramref name="betId"/>.</summary>
    public static WalletRollback Rollback(PlayerRef player, Guid betId, RoundId? round = null) =>
        new(RollbackId(betId), DebitId(betId), player, Brl, round ?? new RoundId(Guid.NewGuid()), betId);

    public static Balance BrlBalance(decimal amount) => new(new Money(amount), Brl);

    /// <summary>Asserts <see cref="WalletOk"/> with the BRL balance <paramref name="balanceAfter"/>.</summary>
    public static void AssertOk(WalletResult result, decimal balanceAfter)
    {
        var ok = Assert.IsType<WalletOk>(result);
        Assert.Equal(BrlBalance(balanceAfter), ok.BalanceAfter);
    }

    /// <summary>Asserts <see cref="WalletDuplicate"/> whose original result is <see cref="WalletOk"/> with <paramref name="originalBalanceAfter"/>.</summary>
    public static void AssertDuplicateOfOk(WalletResult result, decimal originalBalanceAfter)
    {
        var duplicate = Assert.IsType<WalletDuplicate>(result);
        AssertOk(duplicate.OriginalResult, originalBalanceAfter);
    }

    /// <summary>Asserts a balanced double entry: two postings (the player's and the house's) of opposite amounts that sum to zero.</summary>
    public static void AssertBalancedEntry(LedgerEntry entry, PlayerRef player, decimal playerAmount)
    {
        Assert.NotNull(entry);
        Assert.Equal(0m, entry.Postings.Sum(posting => posting.Amount.Value));
        Assert.Equal(2, entry.Postings.Count);
        var playerPosting = Assert.Single(entry.Postings, posting => posting.Account == LedgerAccount.OfPlayer(player, Brl));
        Assert.Equal(playerAmount, playerPosting.Amount.Value);
        var housePosting = Assert.Single(entry.Postings, posting => posting.Account == LedgerAccount.OfHouse(player.OperatorId, Brl));
        Assert.Equal(-playerAmount, housePosting.Amount.Value);
    }
}
