using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Features.Conductor;
using Apogeu.Wallet.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using Xunit;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// The real Host (WebApplicationFactory) in Development on a PostgreSQL database of its own and a flushed Redis: the real
/// rounds conductor of the room <c>demo</c> (<see cref="BettingSala"/>), the real catalog limits (SPEC-0020 seed) and real
/// SPEC-0009 sessions; only <see cref="IOperatorWallet"/> is replaced by a double of the SPEC-0021@2 contract.
/// </summary>
internal sealed class BettingHost : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SharedKeys _keys;
    private readonly string _signingKey;
    private readonly string _redis;
    private readonly BettingSala _sala;
    private bool _disposed;

    private BettingHost(
        string database,
        string redis,
        SharedKeys keys,
        string signingKey,
        IOperatorWallet wallet,
        BettingSala sala,
        Action<IServiceCollection>? services)
    {
        DatabaseConnectionString = database;
        _keys = keys;
        _signingKey = signingKey;
        _redis = redis;
        _sala = sala;
        Wallet = wallet;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.UseSetting("ConnectionStrings:apogeu", database);
            host.UseSetting("ConnectionStrings:redis", redis);
            host.UseSetting(BettingSessions.SettingName, _signingKey);
            const string section = "Rounds:Salas:" + BettingSala.SalaId + ":";
            host.UseSetting(section + "Rtp", sala.Rtp);
            host.UseSetting(section + "Cap", sala.Cap);
            host.UseSetting(section + "GrowthK", sala.GrowthK);
            host.UseSetting(section + "BettingDuration", sala.BettingDuration);
            host.UseSetting(section + "CooldownDuration", sala.CooldownDuration);
            host.UseSetting(section + "ChainLength", sala.ChainLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
            host.ConfigureTestServices(test =>
            {
                test.AddDataProtection().SetApplicationName("Apogeu.Betting.Tests").PersistKeysToFileSystem(keys.Directory);

                // A host that is "killed" (IT-08) stops promptly even with an invocation stuck in the wallet.
                test.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(2));
                test.RemoveAll<IOperatorWallet>();
                test.AddSingleton(wallet);
                services?.Invoke(test);
            });
        });
    }

    public string DatabaseConnectionString { get; }

    /// <summary>The wallet double Betting debits and refunds.</summary>
    public IOperatorWallet Wallet { get; }

    public IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Starts a host on a new database with the <paramref name="wallet"/> double (a <see cref="FakeOperatorWallet"/> by default)
    /// and extra test registrations applied after the Host's own.
    /// </summary>
    public static async Task<BettingHost> StartAsync(
        BettingContainers containers,
        CancellationToken ct,
        IOperatorWallet? wallet = null,
        Action<IServiceCollection>? services = null,
        BettingSala? sala = null)
    {
        ArgumentNullException.ThrowIfNull(containers);
        await FlushRedisAsync(containers.Redis.ConnectionString).ConfigureAwait(false);
        var database = await CreateDatabaseAsync(containers.Postgres.ConnectionString, ct).ConfigureAwait(false);
        var host = new BettingHost(
            database,
            containers.Redis.ConnectionString,
            SharedKeys.Create(),
            BettingSessions.NewSigningKey(),
            wallet ?? new FakeOperatorWallet(),
            sala ?? new BettingSala(),
            services);
        return await StartedAsync(host).ConfigureAwait(false);
    }

    /// <summary>
    /// Another instance of the Host on the same PostgreSQL database, Redis, Data Protection key ring, session key and wallet
    /// double as <paramref name="first"/> (IT-08, IT-11): the instances compete for the leadership of the room.
    /// </summary>
    public static Task<BettingHost> StartPeerAsync(BettingHost first, Action<IServiceCollection>? services = null)
    {
        ArgumentNullException.ThrowIfNull(first);
        var peer = new BettingHost(
            first.DatabaseConnectionString,
            first._redis,
            first._keys.Share(),
            first._signingKey,
            first.Wallet,
            first._sala,
            services);
        return StartedAsync(peer);
    }

    /// <summary>Whether this instance conducts the room <c>demo</c> (SPEC-0008 leadership).</summary>
    public bool IsLeading => Services.GetRequiredService<IRoundsLeadership>().IsLeading(BettingSala.SalaId);

    /// <summary>
    /// "Kills" the instance (IT-08): stops it without waiting for the invocations in flight, at most
    /// <paramref name="timeout"/>; what it had not committed is lost.
    /// </summary>
    public async Task KillAsync(TimeSpan timeout)
    {
        var stopping = DisposeAsync().AsTask();
        await Task.WhenAny(stopping, Task.Delay(timeout, TestContext.Current.CancellationToken)).ConfigureAwait(false);
    }

    private static async Task<BettingHost> StartedAsync(BettingHost host)
    {
        try
        {
            // Starts the host now: migrations, the Development seed, leader election and the conductor run here.
            _ = host._factory.Server;
        }
        catch
        {
            await host.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return host;
    }

    /// <summary>The wallet account of <paramref name="player"/> (operator <c>demo</c>).</summary>
    public static PlayerRef PlayerRef(string player) => new(new OperatorId(BettingSessions.Operator), new PlayerId(player));

    public static Currency Brl => new(BettingSessions.Currency);

    /// <summary>A real SignalR client of <c>/hubs/bets</c> with the session of <paramref name="player"/> (long polling, in memory).</summary>
    public HubConnection CreateBetsConnection(string player, string mode = "demo") =>
        Connection("hubs/bets", BettingSessions.Issue(_signingKey, player, mode));

    /// <summary>A real SignalR client of <c>/hubs/bets</c> without any session.</summary>
    public HubConnection CreateAnonymousBetsConnection() => Connection("hubs/bets", session: null);

    /// <summary>A real SignalR client of <c>/hubs/rounds</c> (SPEC-0008) to follow the phases of the room.</summary>
    public HubConnection CreateRoundsConnection(string player = "watcher") =>
        Connection("hubs/rounds", BettingSessions.Issue(_signingKey, player));

    public HttpClient CreateHttpClient() => _factory.CreateClient();

    /// <summary>Total rows of the tables of the schema <c>betting</c> (migrations history excluded) and how many tables it has.</summary>
    public async Task<(int Tables, long Rows)> BettingRowsAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(DatabaseConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            var tables = new List<string>();
            var list = new NpgsqlCommand(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'betting' AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory'",
                connection);
            await using (list.ConfigureAwait(false))
            {
                var reader = await list.ExecuteReaderAsync(ct).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }

            long rows = 0;
            foreach (var table in tables)
            {
                var count = new NpgsqlCommand($"SELECT count(*) FROM betting.\"{table.Replace("\"", "\"\"", StringComparison.Ordinal)}\"", connection);
                await using (count.ConfigureAwait(false))
                {
                    rows += (long)(await count.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
                }
            }

            return (tables.Count, rows);
        }
    }

    /// <summary>
    /// The status of the bet <paramref name="betId"/> in <c>betting.bets</c>, or <see langword="null"/> when no row was kept.
    /// </summary>
    public async Task<string?> BetStatusAsync(Guid betId, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(DatabaseConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            var command = new NpgsqlCommand("SELECT status FROM betting.bets WHERE id = @id", connection);
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.AddWithValue("id", betId);
                return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
            }
        }
    }

    private HubConnection Connection(string path, string? session)
    {
        var server = _factory.Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                if (session is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(session);
                }
            })
            .Build();
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
        var name = "betting_" + Guid.NewGuid().ToString("N");
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _keys.Release();
        }
    }
}
