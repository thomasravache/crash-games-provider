using StackExchange.Redis;

namespace Apogeu.Operators.Infrastructure.Redis;

/// <summary>
/// The Redis connection of the Operators module, opened on first use (the host starts without reaching Redis) and closed
/// with the host. Keys of the module start with <see cref="KeyPrefix"/>.
/// </summary>
internal sealed class OperatorsRedis(Func<string> connectionString) : IAsyncDisposable
{
    public const string KeyPrefix = "apogeu:operators:";

    private readonly Lazy<Task<ConnectionMultiplexer>> _connection = new(
        () => ConnectionMultiplexer.ConnectAsync(connectionString()),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public async Task<IDatabase> DatabaseAsync()
    {
        var connection = await _connection.Value.ConfigureAwait(false);
        return connection.GetDatabase();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated && _connection.Value.IsCompletedSuccessfully)
        {
            await (await _connection.Value.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);
        }
    }
}
