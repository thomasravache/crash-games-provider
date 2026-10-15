using System.Net;
using Apogeu.Operators.Infrastructure;
using Apogeu.Operators.Infrastructure.Catalog;
using Apogeu.Operators.Infrastructure.Persistence;
using Apogeu.Operators.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;
using static Apogeu.Operators.Tests.Support.CatalogExpectations;

namespace Apogeu.Operators.Tests.Integration;

/// <summary>
/// SPEC-0065 IT-02: the first start of the API in production (ADR-0029) migrates every module and ensures the demo operator,
/// its catalog and its limits when <c>Operators:Demo:Enabled=true</c>, without the Development-only routes.
/// </summary>
[Collection(OperatorsApiDependencies.Name)]
public sealed class ProductionStartupTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    /// <summary>The schemas of the modules with migrations (ADR-0010).</summary>
    private static readonly string[] ModuleSchemas = ["betting", "fairness", "operators", "rounds", "wallet"];

    [Fact(DisplayName = "Dado o Host em Production com banco vazio e Operators:Demo:Enabled=true, quando sobe, então as migrações de todos os módulos foram aplicadas, o operador demo, o catálogo e os limites existem, e /dev/launch-demo responde 404")]
    [Trait("Spec", "SPEC-0065:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenProductionHostOnEmptyDatabaseWithDemoEnabledWhenStartedThenModulesAreMigratedAndDemoOperatorIsReady()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ProductionApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);

        Assert.True(
            host.StartFailure is null,
            $"The Host did not start in Production on an empty database: {host.StartFailure}");

        var states = await host.MigrationStatesAsync(ct);
        var schemas = states.Select(state => state.Schema).ToHashSet(StringComparer.Ordinal);
        var uncovered = ModuleSchemas.Where(schema => !schemas.Contains(schema)).ToList();
        Assert.True(uncovered.Count == 0, $"Setup failed: no DbContext found for the schemas {string.Join(", ", uncovered)}.");
        foreach (var state in states)
        {
            Assert.True(
                state.Applied.Count > 0 && state.Pending.Count == 0,
                $"The migrations of {state.Context} (schema {state.Schema}) were not applied on the start: {state.Applied.Count} applied, pending: {string.Join(", ", state.Pending)}.");
        }

        var demo = await host.WithServiceAsync<IOperatorDirectory, OperatorRecord?>(directory => directory.FindAsync(Demo.Value, ct));
        Assert.True(demo is not null, "The demo operator was not ensured on the start in Production with Operators:Demo:Enabled=true.");

        var catalog = await host.WithServiceAsync<ICatalogStore, CatalogSnapshot>(store => Task.FromResult(store.Load()));
        AssertIsPipa(Assert.Single(catalog.Games, game => game.Code == Pipa));
        Assert.Single(catalog.Enablements, enablement => enablement.Operator == Demo && enablement.Game == Pipa);
        AssertDefaultBrlLimits(Assert.Single(catalog.Limits, limits => limits.Operator == Demo && limits.Limits.Currency == Brl).Limits);

        using var client = host.CreateClient();
        using var response = await client.GetAsync(new Uri("/dev/launch-demo", UriKind.Relative), ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
