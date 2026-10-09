using Apogeu.Operators.Features.Catalog;
using Apogeu.Operators.Infrastructure;
using Apogeu.Operators.Infrastructure.Catalog;
using Apogeu.Operators.Infrastructure.Persistence;
using Apogeu.Operators.Tests.Support;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Apogeu.Operators.Tests.Support.CatalogExpectations;

namespace Apogeu.Operators.Tests.Integration;

/// <summary>IT-01: the catalog seed on real PostgreSQL with the migrations of the schema <c>operators</c> is idempotent (SPEC-0020 §8).</summary>
[Collection(OperatorsPostgres.Name)]
public sealed class CatalogPersistenceTests(PostgreSqlContainerFixture postgres)
{
    [Fact(DisplayName = "Dado PostgreSQL real com as migrações, quando o seed roda duas vezes, então existe um único pipa habilitado para o operador demo com os limites BRL padrão")]
    [Trait("Spec", "SPEC-0020:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealPostgresWhenSeedRunsTwiceThenSinglePipaEnabledForDemoWithDefaultBrlLimits()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);

        // Two "processes", each migrating and seeding, as two starts of the API would.
        for (var run = 0; run < 2; run++)
        {
            await using var provider = BuildProvider(connectionString);
            await provider.MigrateOperatorsDatabaseAsync(ct);
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IOperatorDirectory>().EnsureAsync(
                new OperatorRecord { Id = Demo.Value, Name = "Demo", SalaId = "demo" }, ct);
            await CatalogSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<ICatalogStore>(), ct);
        }

        // A third process reads what is persisted: nothing comes from memory.
        await using var reader = BuildProvider(connectionString);
        await using var readScope = reader.CreateAsyncScope();
        var snapshot = readScope.ServiceProvider.GetRequiredService<ICatalogStore>().Load();

        var pipa = Assert.Single(snapshot.Games, game => game.Code == Pipa);
        AssertIsPipa(pipa);
        Assert.Single(snapshot.Enablements, enablement => enablement.Operator == Demo && enablement.Game == Pipa);
        var brl = Assert.Single(snapshot.Limits, limits => limits.Operator == Demo && limits.Limits.Currency == Brl);
        AssertDefaultBrlLimits(brl.Limits);
        Assert.DoesNotContain(snapshot.Limits, limits => limits.Operator == Demo && limits.Limits.Currency != Brl);
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOperatorsInfrastructure(
            _ => connectionString,
            _ => throw new InvalidOperationException("IT-01 does not use Redis."));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
