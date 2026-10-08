using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Apogeu.ServiceDefaults.Dependencies;

/// <summary>
/// Reports Redis as healthy only when a <c>PING</c> succeeds. The connection is opened on the first check (never at
/// startup) and kept; it does not abort when Redis is down, so it reconnects by itself when Redis comes back.
/// </summary>
internal sealed class RedisHealthCheck(string connectionString) : IHealthCheck, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private ConnectionMultiplexer? _connection;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var connection = await ConnectAsync(cancellationToken).ConfigureAwait(false);
            await connection.GetDatabase().PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("Redis answered PING.");
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Redis is unreachable.", exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _connectGate.Dispose();
    }

    private async Task<ConnectionMultiplexer> ConnectAsync(CancellationToken cancellationToken)
    {
        if (_connection is { } existing)
        {
            return existing;
        }

        await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is null)
            {
                var options = ConfigurationOptions.Parse(connectionString);
                options.AbortOnConnectFail = false;
                _connection = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);
            }

            return _connection;
        }
        finally
        {
            _connectGate.Release();
        }
    }
}
