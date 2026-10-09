using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;

namespace Apogeu.Operators.Tests.Support;

/// <summary>The values SPEC-0020 §2 and §6 fix for the seed, written in the tests independently of the production seed.</summary>
internal static class CatalogExpectations
{
    public static readonly OperatorId Demo = new("demo");
    public static readonly GameCode Pipa = new("pipa");
    public static readonly GameCode Aviao = new("aviao");
    public static readonly Currency Brl = new("BRL");
    public static readonly Currency Usd = new("USD");

    public static readonly Money MinBet = new(1.00m);
    public static readonly Money MaxBet = new(1_000.00m);
    public static readonly Money MaxWin = new(100_000.00m);

    /// <summary>Asserts the full definition of the Pipa (§6).</summary>
    public static void AssertIsPipa(GameDefinition? game)
    {
        Xunit.Assert.NotNull(game);
        Xunit.Assert.Equal(Pipa, game.Code);
        Xunit.Assert.Equal("Pipa", game.Name);
        Xunit.Assert.Equal("game-pipa", game.ClientApp);
        Xunit.Assert.Equal("pipa", game.Theme);
        Xunit.Assert.NotNull(game.DefaultSala);
        Xunit.Assert.Equal(0.97m, game.DefaultSala.Rtp);
        Xunit.Assert.Equal(new Multiplier(10_000.00m), game.DefaultSala.Cap);
        Xunit.Assert.Equal(0.00006, game.DefaultSala.GrowthK, 12);
        Xunit.Assert.Equal(6, game.DefaultSala.BettingSeconds);
        Xunit.Assert.Equal(3, game.DefaultSala.PauseSeconds);
        Xunit.Assert.Equal([Brl], game.Currencies);
    }

    /// <summary>Asserts the default BRL limits (§2): R$ 1,00 / R$ 1.000,00 / R$ 100.000,00.</summary>
    public static void AssertDefaultBrlLimits(BetLimits? limits)
    {
        Xunit.Assert.NotNull(limits);
        Xunit.Assert.Equal(Brl, limits.Currency);
        Xunit.Assert.Equal(MinBet, limits.MinBet);
        Xunit.Assert.Equal(MaxBet, limits.MaxBet);
        Xunit.Assert.Equal(MaxWin, limits.MaxWin);
    }
}
