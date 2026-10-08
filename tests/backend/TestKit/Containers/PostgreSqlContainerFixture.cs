using Testcontainers.PostgreSql;
using Xunit;

namespace Apogeu.TestKit.Containers;

/// <summary>
/// Real PostgreSQL in a container (Testcontainers, ADR-0009). Use it as a class or collection fixture so the container is
/// shared by the tests of the collection; it starts in <see cref="InitializeAsync"/> and is removed in <see cref="DisposeAsync"/>.
/// </summary>
public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(ContainerImages.PostgreSql).Build();

    /// <summary>The connection string for Npgsql of the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
