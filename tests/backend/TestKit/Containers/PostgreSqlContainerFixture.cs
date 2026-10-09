using System.Globalization;
using Testcontainers.PostgreSql;
using Xunit;

namespace Apogeu.TestKit.Containers;

/// <summary>
/// Real PostgreSQL in a container (Testcontainers, ADR-0009). Use it as a class or collection fixture so the container is
/// shared by the tests of the collection; it starts in <see cref="InitializeAsync"/> and is removed in <see cref="DisposeAsync"/>.
/// </summary>
public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgreSqlContainerFixture()
        : this(maxConnections: null)
    {
    }

    private PostgreSqlContainerFixture(int? maxConnections)
    {
        var builder = new PostgreSqlBuilder(ContainerImages.PostgreSql);
        if (maxConnections is { } max)
        {
            builder = builder.WithCommand("-c", "max_connections=" + max.ToString(CultureInfo.InvariantCulture));
        }

        MaxConnections = maxConnections;
        _container = builder.Build();
    }

    /// <summary>The server's <c>max_connections</c> when it was set by <see cref="WithMaxConnections"/>; otherwise the image default.</summary>
    public int? MaxConnections { get; }

    /// <summary>The connection string for Npgsql of the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// A PostgreSQL server that accepts at most <paramref name="maxConnections"/> connections (<c>-c max_connections</c>), for
    /// tests of connection exhaustion; started and disposed by the test itself.
    /// </summary>
    public static PostgreSqlContainerFixture WithMaxConnections(int maxConnections)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConnections, 5);
        return new PostgreSqlContainerFixture(maxConnections);
    }

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
