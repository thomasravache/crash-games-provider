using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Features.Configuration;
using Apogeu.Operators.Infrastructure.Catalog;

namespace Apogeu.Operators.Features.Catalog;

/// <summary>
/// The idempotent seed of SPEC-0020 §2: the game <c>pipa</c>, enabled for the demo operator, and the demo operator's
/// default BRL limits. The demo operator must already exist.
/// </summary>
public static class CatalogSeed
{
    public static readonly Currency Brl = new("BRL");

    /// <summary>The Pipa: app <c>game-pipa</c>, theme <c>pipa</c>, RTP 0.97, cap 10,000x, k 0.00006, betting 6 s, pause 3 s.</summary>
    public static readonly GameDefinition Pipa = new(
        new GameCode("pipa"),
        "Pipa",
        "game-pipa",
        "pipa",
        new SalaDefaults(0.97m, new Multiplier(10_000.00m), 0.00006, BettingSeconds: 6, PauseSeconds: 3),
        [Brl]);

    /// <summary>Default BRL limits of an operator: R$ 1,00 minimum and R$ 1.000,00 maximum bet, R$ 100.000,00 maximum win.</summary>
    public static readonly BetLimits DefaultBrlLimits = new(Brl, new Money(1.00m), new Money(1_000.00m), new Money(100_000.00m));

    /// <summary>Writes the seed through <paramref name="store"/>; running it again changes nothing.</summary>
    public static async Task SeedAsync(ICatalogStore store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);

        var demo = new OperatorId(DemoOperator.Id);
        await store.EnsureGameAsync(Pipa, cancellationToken).ConfigureAwait(false);
        await store.SetGameEnabledAsync(demo, Pipa.Code, enabled: true, cancellationToken).ConfigureAwait(false);
        await store.SetLimitsAsync(demo, DefaultBrlLimits, cancellationToken).ConfigureAwait(false);
    }
}
