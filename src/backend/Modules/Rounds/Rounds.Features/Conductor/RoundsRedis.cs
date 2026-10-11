using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Apogeu.Rounds.Features.Conductor;

/// <summary>
/// The Redis connection of the leader locks (<c>ConnectionStrings:redis</c>), opened on first use and kept for the life of
/// the host. The SignalR backplane has its own connection.
/// </summary>
internal sealed class RoundsRedis(IConfiguration configuration) : IAsyncDisposable
{
    /// <summary>Name of the connection string of the Redis of the rooms (<c>ConnectionStrings:redis</c>).</summary>
    public const string ConnectionName = "redis";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ConnectionMultiplexer? _connection;

    /// <summary>The options of <c>ConnectionStrings:redis</c>, reconnecting in the background instead of failing.</summary>
    public static ConfigurationOptions Options(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"Missing required configuration 'ConnectionStrings:{ConnectionName}'.");
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        return options;
    }

    public async Task<IDatabase> DatabaseAsync() => (await ConnectionAsync().ConfigureAwait(false)).GetDatabase();

    /// <summary>Pub/sub of the same connection: the cash-out requests forwarded to the leader (SPEC-0023).</summary>
    public async Task<ISubscriber> SubscriberAsync() => (await ConnectionAsync().ConfigureAwait(false)).GetSubscriber();

    private async Task<ConnectionMultiplexer> ConnectionAsync()
    {
        if (_connection is { } open)
        {
            return open;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _connection ??= await ConnectionMultiplexer.ConnectAsync(Options(configuration)).ConfigureAwait(false);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _gate.Dispose();
    }
}
