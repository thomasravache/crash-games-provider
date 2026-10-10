using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Betting.Tests.Support;

/// <summary>Real PostgreSQL and Redis (Testcontainers) shared by the Betting integration tests; each test owns a database.</summary>
public sealed class BettingContainers : IAsyncLifetime
{
    public PostgreSqlContainerFixture Postgres { get; } = new();

    public RedisContainerFixture Redis { get; } = new();

    public async ValueTask InitializeAsync() =>
        await Task.WhenAll(Postgres.InitializeAsync().AsTask(), Redis.InitializeAsync().AsTask()).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        await Postgres.DisposeAsync().ConfigureAwait(false);
        await Redis.DisposeAsync().ConfigureAwait(false);
    }
}
