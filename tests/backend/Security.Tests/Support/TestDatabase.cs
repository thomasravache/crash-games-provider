using Npgsql;

namespace Apogeu.Security.Tests.Support;

/// <summary>A database of its own for each test host, in the shared PostgreSQL container.</summary>
internal static class TestDatabase
{
    public static async Task<string> CreateAsync(string serverConnectionString, CancellationToken cancellationToken)
    {
        var name = "security_" + Guid.NewGuid().ToString("N");
        var sql = "CREATE DATABASE \"" + name + "\"";
        var connection = new NpgsqlConnection(serverConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = new NpgsqlCommand(sql, connection);
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = name }.ConnectionString;
    }
}
