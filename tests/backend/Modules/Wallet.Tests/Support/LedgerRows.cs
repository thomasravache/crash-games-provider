using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Infrastructure.Persistence;
using Npgsql;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>Direct reads of the ledger tables of the schema <c>wallet</c> (<see cref="WalletLedgerTables"/>), outside the module code.</summary>
internal static class LedgerRows
{
    private const string Entries = "\"" + WalletLedgerTables.Schema + "\".\"" + WalletLedgerTables.Entries + "\"";
    private const string Postings = "\"" + WalletLedgerTables.Schema + "\".\"" + WalletLedgerTables.Postings + "\"";
    private const string TransactionId = "\"" + WalletLedgerTables.TransactionIdColumn + "\"";
    private const string Amount = "\"" + WalletLedgerTables.AmountColumn + "\"";

    public static string EntriesTable => Entries;

    public static string PostingsTable => Postings;

    public static string TransactionIdColumn => TransactionId;

    /// <summary>Number of ledger entries in the database.</summary>
    public static Task<long> CountEntriesAsync(string connectionString, CancellationToken ct) =>
        ScalarAsync<long>(connectionString, "SELECT count(*) FROM " + Entries, parameter: null, ct);

    /// <summary>Number of ledger postings in the database.</summary>
    public static Task<long> CountPostingsAsync(string connectionString, CancellationToken ct) =>
        ScalarAsync<long>(connectionString, "SELECT count(*) FROM " + Postings, parameter: null, ct);

    /// <summary>Number of entries recorded under <paramref name="id"/>.</summary>
    public static Task<long> CountEntriesAsync(string connectionString, TransactionId id, CancellationToken ct) =>
        ScalarAsync<long>(connectionString, "SELECT count(*) FROM " + Entries + " WHERE " + TransactionId + " = @id", id.Value, ct);

    /// <summary>The signed amounts of the postings of the entry <paramref name="id"/>.</summary>
    public static async Task<IReadOnlyList<decimal>> PostingAmountsAsync(string connectionString, TransactionId id, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            var command = new NpgsqlCommand("SELECT " + Amount + " FROM " + Postings + " WHERE " + TransactionId + " = @id", connection);
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.AddWithValue("id", id.Value);
                var amounts = new List<decimal>();
                var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        amounts.Add(reader.GetDecimal(0));
                    }
                }

                return amounts;
            }
        }
    }

    /// <summary>Runs <paramref name="sql"/> on a new connection with <paramref name="connectionString"/>.</summary>
    public static async Task<int> ExecuteAsync(string connectionString, string sql, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            var command = new NpgsqlCommand(sql, connection);
            await using (command.ConfigureAwait(false))
            {
                return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
    }

    public static async Task<T> ScalarAsync<T>(string connectionString, string sql, string? parameter, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            var command = new NpgsqlCommand(sql, connection);
            await using (command.ConfigureAwait(false))
            {
                if (parameter is not null)
                {
                    command.Parameters.AddWithValue("id", parameter);
                }

                return (T)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
            }
        }
    }
}
