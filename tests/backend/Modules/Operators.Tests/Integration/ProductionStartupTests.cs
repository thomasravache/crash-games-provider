using System.Net;
using Apogeu.Operators.Infrastructure;
using Apogeu.Operators.Infrastructure.Catalog;
using Apogeu.Operators.Infrastructure.Persistence;
using Apogeu.Operators.Tests.Support;
using Apogeu.TestKit.Containers;
using Npgsql;
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

    /// <summary>The envelope tables of the Wolverine durable storage (inbox, outbox, dead letters).</summary>
    private static readonly string[] WolverineTables = ["wolverine_incoming_envelopes", "wolverine_outgoing_envelopes", "wolverine_dead_letters"];

    [Fact(DisplayName = "Dado o Host em Production com banco vazio e Operators:Demo:Enabled=true, Database:MigrateOnStartup=true, quando sobe, então as migrações de todos os módulos (e o armazenamento do Wolverine) foram aplicadas, o operador demo, o catálogo e os limites existem, e /dev/launch-demo responde 404")]
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

        // §6 (Emenda v2): the message storage of Wolverine (schema wolverine, transactional outbox) is created on the start too.
        var missingTables = new List<string>();
        foreach (var table in WolverineTables)
        {
            if (!await TableExistsAsync(host.Database, "wolverine", table, ct))
            {
                missingTables.Add(table);
            }
        }

        Assert.True(
            missingTables.Count == 0,
            $"The message storage of Wolverine was not created on the start in Production: missing wolverine.{string.Join(", wolverine.", missingTables)}.");

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

    [Fact(DisplayName = "Dado o Host em Production com banco vazio e sem Database:MigrateOnStartup, quando sobe, então nada é migrado (as migrações de produção real ficam com o pipeline, ADR-0010)")]
    [Trait("Spec", "SPEC-0065:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenProductionHostWithoutMigrateOnStartupWhenStartedThenNothingIsMigrated()
    {
        var ct = TestContext.Current.CancellationToken;

        // Whether it starts or not on a database without schema is not the point: only that it did not migrate.
        await using var host = await ProductionApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct, migrateOnStartup: false);

        var histories = await CountAsync(
            host.Database, "SELECT count(*) FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory'", ct);
        Assert.True(histories == 0, $"The Host in Production without Database:MigrateOnStartup migrated {histories} module schema(s).");
        foreach (var table in WolverineTables)
        {
            Assert.False(
                await TableExistsAsync(host.Database, "wolverine", table, ct),
                $"The Host in Production without Database:MigrateOnStartup created wolverine.{table}.");
        }
    }

    private static async Task<long> CountAsync(string database, string sql, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(database);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            var command = new NpgsqlCommand(sql, connection);
            await using (command.ConfigureAwait(false))
            {
                return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    private static async Task<bool> TableExistsAsync(string database, string schema, string table, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(database);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            var command = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL", connection);
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.AddWithValue("name", $"{schema}.{table}");
                return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is true;
            }
        }
    }
}
