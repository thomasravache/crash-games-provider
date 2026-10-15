using Apogeu.Deploy.Tests.Support;
using Apogeu.Repository.Tests.Support;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Apogeu.Deploy.Tests.Integration;

/// <summary>
/// SPEC-0066 IT-02: <c>tools/deploy/backup</c> dumps the database <c>apogeu</c> of a PostgreSQL with data in the schemas
/// of the modules (Testcontainers, pointed by APOGEU_POSTGRES_CONTAINER) into APOGEU_BACKUP_DIR and prints the path of
/// the file; <c>tools/deploy/restore &lt;file&gt;</c> loads it into a clean PostgreSQL and the row counts of every table
/// match. The backup keeps 7 days: older dumps of the directory go, newer ones and other files stay.
/// </summary>
public sealed class BackupRestoreTests : IAsyncLifetime
{
    private const string Database = "apogeu";

    /// <summary>Schemas of the modules (and of the Wolverine storage), with a different number of rows each.</summary>
    private static readonly (string Schema, int Rows)[] ModuleTables =
    [
        ("rounds", 37), ("betting", 211), ("wallet", 59), ("fairness", 13), ("operators", 3), ("backoffice", 7), ("wolverine", 101),
    ];

    private readonly PostgreSqlContainer _source = new PostgreSqlBuilder(DockerCli.PostgreSqlImage).Build();
    private readonly PostgreSqlContainer _target = new PostgreSqlBuilder(DockerCli.PostgreSqlImage).Build();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "apogeu-test-it02-" + Guid.NewGuid().ToString("N"));

    private string BackupDir => Path.Combine(_root, "backups");

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_source.StartAsync(), _target.StartAsync()).ConfigureAwait(false);
        Directory.CreateDirectory(BackupDir);
        await SeedAsync(_source.GetConnectionString()).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _source.DisposeAsync().ConfigureAwait(false);
        await _target.DisposeAsync().ConfigureAwait(false);
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact(DisplayName = "Dado um Postgres com dados, quando o backup roda e o restore carrega o arquivo num Postgres limpo, então as contagens das tabelas coincidem")]
    [Trait("Category", "Integration")]
    [Trait("Spec", "SPEC-0066:IT-02")]
    public async Task GivenAPostgresWithDataWhenBackedUpAndRestoredIntoACleanOneThenTheCountsMatch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var expected = await CountsAsync(WithDatabase(_source.GetConnectionString()), cancellationToken);
        Assert.Equal(ModuleTables.Length, expected.Count);
        Assert.False(await DatabaseExistsAsync(_target.GetConnectionString(), cancellationToken), "the target PostgreSQL is not clean");

        var backup = await RunAsync(DeployScripts.Backup, [], _source, cancellationToken);

        Assert.True(backup.Succeeded, "backup failed. " + backup.Describe());
        var dump = DumpPathOf(backup);
        Assert.True(File.Exists(dump), $"backup printed '{dump}', which is not a file. " + backup.Describe());
        Assert.Equal(Path.GetFullPath(BackupDir), Path.GetDirectoryName(Path.GetFullPath(dump)));
        Assert.StartsWith("apogeu-", Path.GetFileName(dump), StringComparison.Ordinal);
        Assert.True(new FileInfo(dump).Length > 0, "empty dump");

        var restore = await RunAsync(DeployScripts.Restore, [dump], _target, cancellationToken);

        Assert.True(restore.Succeeded, "restore failed. " + restore.Describe());
        var restored = await CountsAsync(WithDatabase(_target.GetConnectionString()), cancellationToken);
        Assert.Equal(expected, restored);
    }

    [Fact(DisplayName = "Dados dumps com mais e menos de 7 dias, quando o backup roda, então só os mais antigos que 7 dias são apagados")]
    [Trait("Category", "Integration")]
    [Trait("Spec", "SPEC-0066:IT-02")]
    public async Task GivenOldAndRecentDumpsWhenBackupRunsThenOnlyTheOlderThanSevenDaysGo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var old = Path.Combine(BackupDir, "apogeu-20260101T000000Z.dump");
        var recent = Path.Combine(BackupDir, "apogeu-20260102T000000Z.dump");
        var unrelated = Path.Combine(BackupDir, "notes.txt");
        foreach (var file in new[] { old, recent, unrelated })
        {
            await File.WriteAllTextAsync(file, "x", cancellationToken);
        }

        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-8));
        File.SetLastWriteTimeUtc(unrelated, DateTime.UtcNow.AddDays(-8));
        File.SetLastWriteTimeUtc(recent, DateTime.UtcNow.AddDays(-6));

        var backup = await RunAsync(DeployScripts.Backup, [], _source, cancellationToken);

        Assert.True(backup.Succeeded, "backup failed. " + backup.Describe());
        Assert.True(File.Exists(DumpPathOf(backup)), "the new dump is missing. " + backup.Describe());
        Assert.False(File.Exists(old), "a dump older than 7 days was kept");
        Assert.True(File.Exists(recent), "a dump newer than 7 days was removed");
        Assert.True(File.Exists(unrelated), "a file that is not a dump was removed");
    }

    /// <summary>The last non-empty line of the standard output: the path of the dump.</summary>
    private static string DumpPathOf(ProcessResult backup) =>
        backup.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? string.Empty;

    private static string WithDatabase(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Database = Database }.ConnectionString;

    private static async Task SeedAsync(string serverConnectionString)
    {
        await ExecuteAsync(serverConnectionString, $"CREATE DATABASE {Database}").ConfigureAwait(false);
        var sql = string.Join(
            '\n',
            ModuleTables.Select(table =>
                $"""
                 CREATE SCHEMA {table.Schema};
                 CREATE TABLE {table.Schema}.items (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, payload jsonb NOT NULL, raw bytea, created_at timestamptz NOT NULL DEFAULT now());
                 INSERT INTO {table.Schema}.items (payload, raw) SELECT jsonb_build_object('n', n, 'schema', '{table.Schema}'), sha256(n::text::bytea) FROM generate_series(1, {table.Rows}) AS n;
                 """));
        await ExecuteAsync(WithDatabase(serverConnectionString), sql).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        var connection = new NpgsqlConnection(connectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
#pragma warning disable CA2100 // fixed seed script of the test, no input
            var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<bool> DatabaseExistsAsync(string serverConnectionString, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(serverConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @name", connection);
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.AddWithValue("name", Database);
                return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! > 0;
            }
        }
    }

    /// <summary>Row count of every user table of the database, by schema-qualified name.</summary>
    private static async Task<SortedDictionary<string, long>> CountsAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var tables = new List<string>();
            var list = new NpgsqlCommand(
                "SELECT quote_ident(schemaname) || '.' || quote_ident(relname) FROM pg_stat_user_tables ORDER BY 1", connection);
            await using (list.ConfigureAwait(false))
            {
                var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }

            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var table in tables)
            {
#pragma warning disable CA2100 // table names come from the catalogue, quoted by quote_ident
                var count = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection);
#pragma warning restore CA2100
                await using (count.ConfigureAwait(false))
                {
                    counts[table] = (long)(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
                }
            }

            return counts;
        }
    }

    private Task<ProcessResult> RunAsync(string script, IEnumerable<string> arguments, PostgreSqlContainer container, CancellationToken cancellationToken) =>
        DeployScripts.RunAsync(
            script,
            arguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DeployScripts.DeployDirVariable] = _root,
                [DeployScripts.BackupDirVariable] = BackupDir,
                [DeployScripts.PostgresContainerVariable] = container.Id,
                [DeployScripts.ProjectVariable] = "apogeu-test-it02",
            },
            cancellationToken);
}
