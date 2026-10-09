using Apogeu.Operators.Features.Catalog;
using Apogeu.Operators.Tests.Support;
using Xunit;
using static Apogeu.Operators.Tests.Support.CatalogExpectations;

namespace Apogeu.Operators.Tests.Unit;

/// <summary>UT-01: the seeded catalog finds the Pipa with its full definition and nothing for an unknown game (SPEC-0020 §6).</summary>
public sealed class GameCatalogTests
{
    [Fact(DisplayName = "Dado o catálogo semeado, quando Find(\"pipa\"), então retorna a definição completa da Pipa")]
    [Trait("Spec", "SPEC-0020:UT-01")]
    public async Task GivenSeededCatalogWhenFindingPipaThenReturnsFullDefinition()
    {
        var store = new InMemoryCatalogStore();
        await CatalogSeed.SeedAsync(store, TestContext.Current.CancellationToken);
        var catalog = new GameCatalog(store);

        var pipa = catalog.Find(Pipa);

        AssertIsPipa(pipa);
        Assert.True(catalog.IsEnabledFor(Demo, Pipa), "The seed enables the Pipa for the demo operator (SPEC-0020 §2).");
    }

    [Fact(DisplayName = "Dado o catálogo semeado, quando Find(\"aviao\"), então retorna nulo")]
    [Trait("Spec", "SPEC-0020:UT-01")]
    public async Task GivenSeededCatalogWhenFindingUnknownGameThenReturnsNull()
    {
        var store = new InMemoryCatalogStore();
        await CatalogSeed.SeedAsync(store, TestContext.Current.CancellationToken);
        var catalog = new GameCatalog(store);

        Assert.Null(catalog.Find(Aviao));
        Assert.False(catalog.IsEnabledFor(Demo, Aviao), "A game that does not exist is not enabled for anyone.");
    }
}
