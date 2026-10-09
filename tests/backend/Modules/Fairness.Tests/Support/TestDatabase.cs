using Npgsql;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>A database of its own for each test, in the shared PostgreSQL container.</summary>
internal static class TestDatabase
{
    public static async Task<string> CreateAsync(string serverConnectionString, CancellationToken cancellationToken)
    {
        var name = "fairness_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnection(serverConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = name }.ConnectionString;
    }
}
