using System.Data.Common;
using Apogeu.Rounds.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Apogeu.Rounds.Tests.Unit;

/// <summary>
/// From the G4 delta review of IT-07 (6a75505): the Rounds DbContext gets a Npgsql pool of its own bounded by
/// <c>Rounds:MaxPoolSize</c> (<c>RoundsInfrastructure.WithMaxPoolSize</c>), read through the connection string of the
/// registered DbContext. Tagged with the closest unit test of the snapshot/sync behavior (UT-04): the plan has no UT for IMP-02.
/// </summary>
public sealed class RoundsPoolSizeTests
{
    private const string Base =
        "Host=db.example;Port=5433;Database=apogeu;Username=apogeu;Password=secret;Timeout=7;Command Timeout=11;Application Name=rounds-tests;SSL Mode=Disable";

    [Fact(DisplayName = "Dado Rounds:MaxPoolSize 10, quando a string de conexão não define pool, então o DbContext do Rounds usa Maximum Pool Size 10")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public void GivenMaxPoolSizeWhenConnectionStringHasNoPoolThenRoundsUsesIt()
    {
        var pool = new NpgsqlConnectionStringBuilder(RoundsConnectionString(Base, 10));

        Assert.Equal(10, pool.MaxPoolSize);
        Assert.Equal(0, pool.MinPoolSize);
    }

    [Theory(DisplayName = "Dado Rounds:MaxPoolSize, quando a string de conexão já define pool, então o máximo é o configurado e o mínimo nunca o ultrapassa")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    [InlineData(200, 50, 10, 10)]
    [InlineData(200, 2, 10, 2)]
    [InlineData(5, 0, 10, 0)]
    [InlineData(100, 10, 10, 10)]
    public void GivenConfiguredPoolWhenAppliedThenMaxIsReplacedAndMinIsCapped(int max, int min, int configured, int expectedMin)
    {
        var connectionString = $"{Base};Maximum Pool Size={max};Minimum Pool Size={min}";

        var pool = new NpgsqlConnectionStringBuilder(RoundsConnectionString(connectionString, configured));

        Assert.Equal(configured, pool.MaxPoolSize);
        Assert.Equal(expectedMin, pool.MinPoolSize);
    }

    [Fact(DisplayName = "Dado Rounds:MaxPoolSize, quando aplicado à string de conexão, então todas as outras chaves ficam inalteradas")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public void GivenMaxPoolSizeWhenAppliedThenEveryOtherKeyIsKept()
    {
        var connectionString = $"{Base};Maximum Pool Size=200;Minimum Pool Size=50";

        var before = Keys(connectionString);
        var after = Keys(RoundsConnectionString(connectionString, 10));

        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, value) in before.Where(pair => !IsPoolKey(pair.Key)))
        {
            Assert.True(
                string.Equals(value, after[key], StringComparison.Ordinal),
                $"The key '{key}' changed from '{value}' to '{after[key]}'.");
        }
    }

    /// <summary>The connection string of the DbContext registered by <c>AddRoundsInfrastructure</c>.</summary>
    private static string RoundsConnectionString(string connectionString, int maxPoolSize)
    {
        var services = new ServiceCollection();
        services.AddRoundsInfrastructure(_ => connectionString, _ => maxPoolSize);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var contextType = typeof(RoundsInfrastructure).Assembly.GetType("Apogeu.Rounds.Infrastructure.Persistence.RoundsDbContext", throwOnError: true)!;
        var db = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
        return db.Database.GetConnectionString() ?? throw new InvalidOperationException("The Rounds DbContext has no connection string.");
    }

    /// <summary>The keys of a connection string in Npgsql's canonical form, with their values.</summary>
    private static Dictionary<string, string> Keys(string connectionString)
    {
        var canonical = new DbConnectionStringBuilder { ConnectionString = new NpgsqlConnectionStringBuilder(connectionString).ConnectionString };
        return canonical.Keys.Cast<string>().ToDictionary(
            key => key.ToUpperInvariant(),
            key => Convert.ToString(canonical[key], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            StringComparer.Ordinal);
    }

    private static bool IsPoolKey(string key) => key is "MAXIMUM POOL SIZE" or "MINIMUM POOL SIZE";
}
