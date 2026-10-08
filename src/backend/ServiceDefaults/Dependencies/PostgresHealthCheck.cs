using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Apogeu.ServiceDefaults.Dependencies;

/// <summary>Reports PostgreSQL as healthy only when <c>SELECT 1</c> succeeds (a real round trip, not a socket probe).</summary>
internal sealed class PostgresHealthCheck(string connectionString) : IHealthCheck, IAsyncDisposable
{
    // Creating a data source parses the connection string but does not connect.
    private readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(connectionString);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var command = _dataSource.CreateCommand("SELECT 1");
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }

            return HealthCheckResult.Healthy("PostgreSQL answered SELECT 1.");
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "PostgreSQL is unreachable.", exception);
        }
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
