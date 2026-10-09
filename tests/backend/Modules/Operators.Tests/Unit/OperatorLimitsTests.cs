using System.Diagnostics;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Features.Catalog;
using Apogeu.Operators.Tests.Support;
using Xunit;
using static Apogeu.Operators.Tests.Support.CatalogExpectations;

namespace Apogeu.Operators.Tests.Unit;

/// <summary>UT-02 and UT-03: bet limits per operator and currency and their in-memory cache by version (SPEC-0020 §5, §6).</summary>
public sealed class OperatorLimitsTests
{
    private const int Reads = 10_000;

    [Fact(DisplayName = "Dado o operador demo, quando Get(op, BRL), então 1,00 / 1.000,00 / 100.000,00")]
    [Trait("Spec", "SPEC-0020:UT-02")]
    public async Task GivenDemoOperatorWhenGettingBrlLimitsThenDefaultLimits()
    {
        var limits = await SeededLimitsAsync(new InMemoryCatalogStore());

        AssertDefaultBrlLimits(limits.Get(Demo, Brl));
    }

    [Fact(DisplayName = "Dado o operador demo, quando Get(op, USD), então lança CURRENCY_NOT_ENABLED")]
    [Trait("Spec", "SPEC-0020:UT-02")]
    public async Task GivenDemoOperatorWhenGettingUsdLimitsThenCurrencyNotEnabled()
    {
        var limits = await SeededLimitsAsync(new InMemoryCatalogStore());

        var error = Assert.Throws<CurrencyNotEnabledException>(() => limits.Get(Demo, Usd));

        Assert.Equal("CURRENCY_NOT_ENABLED", error.Code);
        Assert.Equal(Demo, error.Operator);
        Assert.Equal(Usd, error.Currency);
    }

    [Fact(DisplayName = "Dadas 10.000 leituras de limites, quando medidas, então a média é ≤ 1 ms e o catálogo é lido uma vez só (cache em memória)")]
    [Trait("Spec", "SPEC-0020:UT-03")]
    public async Task GivenTenThousandReadsWhenMeasuredThenAverageIsAtMostOneMillisecondAndCached()
    {
        var store = new InMemoryCatalogStore();
        var limits = await SeededLimitsAsync(store);
        _ = limits.Get(Demo, Brl); // Warm-up: the first read may load the catalog.
        var loadsAfterWarmUp = store.LoadCount;

        var watch = Stopwatch.StartNew();
        for (var i = 0; i < Reads; i++)
        {
            _ = limits.Get(Demo, Brl);
        }

        watch.Stop();
        var averageMs = watch.Elapsed.TotalMilliseconds / Reads;

        Assert.True(averageMs <= 1.0, $"Expected an average read of at most 1 ms (SPEC-0020 §5), measured {averageMs:F6} ms.");
        Assert.True(
            store.LoadCount == loadsAfterWarmUp,
            $"Expected the limits to be served from the in-memory cache while the version does not change; the store was loaded {store.LoadCount - loadsAfterWarmUp} more time(s) in {Reads} reads.");
    }

    [Fact(DisplayName = "Dados limites em cache, quando a versão muda, então a leitura seguinte reflete os limites novos")]
    [Trait("Spec", "SPEC-0020:UT-03")]
    public async Task GivenCachedLimitsWhenVersionChangesThenNextReadReflectsNewLimits()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryCatalogStore();
        var limits = await SeededLimitsAsync(store);
        AssertDefaultBrlLimits(limits.Get(Demo, Brl));
        var versionBefore = store.Version;

        var changed = new BetLimits(Brl, new Money(2.00m), new Money(500.00m), new Money(50_000.00m));
        await store.SetLimitsAsync(Demo, changed, ct);
        Assert.NotEqual(versionBefore, store.Version);

        var read = limits.Get(Demo, Brl);

        Assert.Equal(changed, read);
    }

    private static async Task<OperatorLimits> SeededLimitsAsync(InMemoryCatalogStore store)
    {
        await CatalogSeed.SeedAsync(store, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return new OperatorLimits(store);
    }
}
