using Npgsql;
using StackExchange.Redis;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// The instances of one integration test: a database of its own, a clean Redis (no leader lock nor backplane traffic left
/// by a previous test) and one Data Protection key ring shared by the instances.
/// </summary>
internal sealed class RoundsCluster : IAsyncDisposable
{
    private readonly RoundsContainers _containers;
    private readonly DirectoryInfo _keys = Directory.CreateTempSubdirectory("apogeu-rounds-keys-");
    private readonly List<RoundsHost> _hosts = [];
    private int _started;

    private RoundsCluster(RoundsContainers containers, string databaseConnectionString, SalaSettings sala)
    {
        _containers = containers;
        DatabaseConnectionString = databaseConnectionString;
        Sala = sala;
    }

    public string DatabaseConnectionString { get; }

    public SalaSettings Sala { get; }

    /// <summary>The shared Redis (leader locks and SignalR backplane).</summary>
    public string RedisConnectionString => _containers.Redis.ConnectionString;

    public static async Task<RoundsCluster> CreateAsync(RoundsContainers containers, SalaSettings? sala, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(containers);
        await FlushRedisAsync(containers.Redis.ConnectionString).ConfigureAwait(false);
        var database = await CreateDatabaseAsync(containers.Postgres.ConnectionString, ct).ConfigureAwait(false);
        return new RoundsCluster(containers, database, sala ?? new SalaSettings());
    }

    /// <summary>Starts a new instance of the Host.</summary>
    public RoundsHost StartHost() => StartHost(new RoundsHostSetup { Salas = new Dictionary<string, SalaSettings>(StringComparer.Ordinal) { [SalaSettings.SalaId] = Sala } });

    /// <summary>Starts a new instance of the Host with its own <paramref name="setup"/>.</summary>
    public RoundsHost StartHost(RoundsHostSetup setup)
    {
        var host = new RoundsHost(
            $"host-{Interlocked.Increment(ref _started)}",
            DatabaseConnectionString,
            _containers.Redis.ConnectionString,
            _keys,
            setup);
        _hosts.Add(host);
        host.Start();
        return host;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }

        try
        {
            _keys.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must not fail the run.
        }
    }

    private static async Task FlushRedisAsync(string connectionString)
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(connectionString + ",allowAdmin=true").ConfigureAwait(false);
        await using (redis.ConfigureAwait(false))
        {
            foreach (var endpoint in redis.GetEndPoints())
            {
                await redis.GetServer(endpoint).FlushAllDatabasesAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<string> CreateDatabaseAsync(string serverConnectionString, CancellationToken ct)
    {
        var name = "rounds_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnection(serverConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        return new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = name }.ConnectionString;
    }
}
