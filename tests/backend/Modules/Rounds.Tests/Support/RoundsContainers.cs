using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>Real PostgreSQL and Redis (Testcontainers) shared by the Rounds integration tests; each test owns a database.</summary>
public sealed class RoundsContainers : IAsyncLifetime
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
