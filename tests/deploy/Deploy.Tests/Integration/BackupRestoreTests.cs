using Apogeu.Deploy.Tests.Support;
using Apogeu.Repository.Tests.Support;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Apogeu.Deploy.Tests.Integration;

/// <summary>
/// SPEC-0066 IT-02 (Emenda v3): <c>tools/deploy/backup</c> dumps the database <c>apogeu</c> of a PostgreSQL with data in
/// the schemas of the modules (Testcontainers, pointed by APOGEU_POSTGRES_CONTAINER) and archives the Data Protection
/// keys of the volume of APOGEU_KEYS_VOLUME into APOGEU_BACKUP_DIR, as <c>apogeu-&lt;stamp&gt;.dump</c> and
/// <c>apogeu-keys-&lt;stamp&gt;.tar.gz</c> with the same stamp, and prints the path of the dump;
/// <c>tools/deploy/restore &lt;dump&gt;</c> loads the dump into a clean PostgreSQL and the keys archive beside it into a
/// clean volume: the row counts of every table match and the keys are identical (content and owner, the api user 1654).
/// Without the keys the unrevealed seeds stay unreadable. A corrupted keys archive, or one without keys, is refused
/// before the database and the volume are touched, and the current keys stay (Emendas v4 and v5); the keys are imported
/// before the database (Emenda v5), and the dump is checked before the keys are replaced (Emenda v6). The backup keeps 7 days: older files of the directory go,
/// newer ones and other files stay.
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
    private readonly string _sourceKeys = "apogeu-test-it02-keys-" + Guid.NewGuid().ToString("N"); // gitleaks:allow (volume name, not a secret)
    private readonly string _targetKeys = "apogeu-test-it02-keys-" + Guid.NewGuid().ToString("N"); // gitleaks:allow (volume name, not a secret)

    private string BackupDir => Path.Combine(_root, "backups");

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_source.StartAsync(), _target.StartAsync()).ConfigureAwait(false);
        Directory.CreateDirectory(BackupDir);
        await SeedAsync(_source.GetConnectionString()).ConfigureAwait(false);
        await SeedKeysAsync(_sourceKeys).ConfigureAwait(false);
        await RunCheckedAsync(["volume", "create", _targetKeys]).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _source.DisposeAsync().ConfigureAwait(false);
        await _target.DisposeAsync().ConfigureAwait(false);
        await DockerCli.RunAsync(["volume", "rm", "--force", _sourceKeys, _targetKeys], CancellationToken.None).ConfigureAwait(false);
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact(DisplayName = "Dado um Postgres com dados e o volume de chaves, quando o backup roda e o restore carrega num Postgres e num volume limpos, então as contagens coincidem e as chaves são idênticas")]
    [Trait("Category", "Integration")]
    [Trait("Spec", "SPEC-0066:IT-02")]
    public async Task GivenAPostgresWithDataWhenBackedUpAndRestoredIntoACleanOneThenTheCountsMatch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var expected = await CountsAsync(WithDatabase(_source.GetConnectionString()), cancellationToken);
        Assert.Equal(ModuleTables.Length, expected.Count);
        Assert.False(await DatabaseExistsAsync(_target.GetConnectionString(), cancellationToken), "the target PostgreSQL is not clean");
        var expectedKeys = await KeysOfAsync(_sourceKeys);
        Assert.Equal(3, expectedKeys.Count);
        Assert.Empty(await KeysOfAsync(_targetKeys));

        var backup = await RunAsync(DeployScripts.Backup, [], _source, _sourceKeys, cancellationToken);

        Assert.True(backup.Succeeded, "backup failed. " + backup.Describe());
        var dump = DumpPathOf(backup);
        Assert.True(File.Exists(dump), $"backup printed '{dump}', which is not a file. " + backup.Describe());
        Assert.Equal(Path.GetFullPath(BackupDir), Path.GetDirectoryName(Path.GetFullPath(dump)));
        Assert.StartsWith("apogeu-", Path.GetFileName(dump), StringComparison.Ordinal);
        Assert.False(Path.GetFileName(dump).StartsWith("apogeu-keys-", StringComparison.Ordinal), "backup printed the keys archive, not the dump");
        Assert.True(new FileInfo(dump).Length > 0, "empty dump");
        var keysArchive = KeysArchiveOf(dump);
        Assert.True(File.Exists(keysArchive), $"no keys archive {Path.GetFileName(keysArchive)} beside the dump. " + backup.Describe());
        Assert.True(new FileInfo(keysArchive).Length > 0, "empty keys archive");

        var restore = await RunAsync(DeployScripts.Restore, [dump], _target, _targetKeys, cancellationToken);

        Assert.True(restore.Succeeded, "restore failed. " + restore.Describe());
        var restored = await CountsAsync(WithDatabase(_target.GetConnectionString()), cancellationToken);
        Assert.Equal(expected, restored);
        Assert.Equal(expectedKeys, await KeysOfAsync(_targetKeys));
    }

    [Fact(DisplayName = "Dado um arquivo de chaves corrompido ao lado do dump, quando o restore roda, então recusa antes de tocar no banco e no volume, e as chaves atuais continuam intactas")]
    [Trait("Category", "Integration")]
    [Trait("Spec", "SPEC-0066:IT-02")]
    public async Task GivenACorruptedKeysArchiveWhenRestoredThenNothingIsTouched()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var backup = await RunAsync(DeployScripts.Backup, [], _source, _sourceKeys, cancellationToken);
        Assert.True(backup.Succeeded, "backup failed. " + backup.Describe());
        var dump = DumpPathOf(backup);
        var keysArchive = KeysArchiveOf(dump);
        Assert.True(File.Exists(keysArchive), "no keys archive beside the dump. " + backup.Describe());

        // A truncated archive: the gzip stream ends in the middle (copy cut short, disk full).
        var bytes = await File.ReadAllBytesAsync(keysArchive, cancellationToken);
        await File.WriteAllBytesAsync(keysArchive, bytes[..(bytes.Length / 2)], cancellationToken);
        // The keys the api of the target uses now.
        await SeedKeysAsync(_targetKeys);
        var currentKeys = await KeysOfAsync(_targetKeys);

        var restore = await RunAsync(DeployScripts.Restore, [dump], _target, _targetKeys, cancellationToken);

        Assert.False(restore.Succeeded, "restore accepted a corrupted keys archive. " + restore.Describe());
        Assert.True(restore.ExitCode > 0, "restore did not run. " + restore.Describe());
        Assert.False(await DatabaseExistsAsync(_target.GetConnectionString(), cancellationToken), "restore touched the database before refusing the keys archive");
        Assert.Equal(currentKeys, await KeysOfAsync(_targetKeys));
    }

    [Fact(DisplayName = "Dado um arquivo de chaves válido mas sem nenhuma chave, quando o restore roda, então recusa antes de tocar no banco e no volume")]
    [Trait("Category", "Integration")]
    [Trait("Spec", "SPEC-0066:IT-02")]
    public async Task GivenAKeysArchiveWithoutKeysWhenRestoredThenNothingIsTouched()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var backup = await RunAsync(DeployScripts.Backup, [], _source, _sourceKeys, cancellationToken);
        Assert.True(backup.Succeeded, "backup failed. " + backup.Describe());
        var dump = DumpPathOf(backup);
        var keysArchive = KeysArchiveOf(dump);

        // A well-formed tar.gz of an empty directory: gzip and tar accept it, but there is no key in it.
        var empty = Path.Combine(_root, "empty-keys");
        Directory.CreateDirectory(empty);
        File.Delete(keysArchive);
        var tar = await ProcessRunner.RunAsync("tar", ["-czf", keysArchive, "-C", empty, "."], _root, ProcessRunner.ShortTimeout, cancellationToken: cancellationToken);
        Assert.True(tar.Succeeded, tar.Describe());
        await SeedKeysAsync(_targetKeys);
        var currentKeys = await KeysOfAsync(_targetKeys);

        var restore = await RunAsync(DeployScripts.Restore, [dump], _target, _targetKeys, cancellationToken);

        Assert.False(restore.Succeeded, "restore accepted a keys archive without keys. " + restore.Describe());
        Assert.True(restore.ExitCode > 0, "restore did not run. " + restore.Describe());
        Assert.False(await DatabaseExistsAsync(_target.GetConnectionString(), cancellationToken), "restore touched the database before refusing the keys archive");
        Assert.Equal(currentKeys, await KeysOfAsync(_targetKeys));
    }

    [Fact(DisplayName = "Dada uma importação das chaves que falha, quando o restore roda, então o banco não foi restaurado (as chaves vêm antes do banco)")]
    [Trait("Category", "Integration")]
    [Trait("Spec", "SPEC-0066:IT-02")]
    public async Task GivenTheKeysImportFailingWhenRestoredThenTheDatabaseIsNotRestored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var backup = await RunAsync(DeployScripts.Backup, [], _source, _sourceKeys, cancellationToken);
        Assert.True(backup.Succeeded, "backup failed. " + backup.Describe());

        // A volume that exists (docker volume inspect works) but cannot be mounted: a bind to a directory that does not
        // exist. The archive is valid; only writing the keys fails.
        var broken = "apogeu-test-it02-broken-" + Guid.NewGuid().ToString("N");
        await RunCheckedAsync(
        [
            "volume", "create", "--driver", "local", "--opt", "type=none", "--opt", "o=bind",
            "--opt", "device=" + Path.Combine(_root, "does-not-exist"), broken,
        ]);
        try
        {
            var restore = await RunAsync(DeployScripts.Restore, [DumpPathOf(backup)], _target, broken, cancellationToken);

            Assert.False(restore.Succeeded, "restore succeeded with a keys volume that cannot be written. " + restore.Describe());
            Assert.False(
                await DatabaseExistsAsync(_target.GetConnectionString(), cancellationToken),
                "the database was restored although importing the keys failed: the keys must come first. " + restore.Describe());
        }
        finally
        {
            await DockerCli.RunAsync(["volume", "rm", "--force", broken], CancellationToken.None);
        }
    }

    [Fact(DisplayName = "Dado um dump corrompido ao lado de um arquivo de chaves válido, quando o restore roda, então recusa antes de trocar as chaves e não cria o banco")]
    [Trait("Category", "Integration")]
    [Trait("Spec", "SPEC-0066:IT-02")]
    public async Task GivenACorruptedDumpWithValidKeysWhenRestoredThenTheKeysStayAndNoDatabaseIsCreated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var backup = await RunAsync(DeployScripts.Backup, [], _source, _sourceKeys, cancellationToken);
        Assert.True(backup.Succeeded, "backup failed. " + backup.Describe());
        var dump = DumpPathOf(backup);
        Assert.True(File.Exists(KeysArchiveOf(dump)), "no keys archive beside the dump. " + backup.Describe());

        // A truncated dump (copy cut short, disk full) beside a valid keys archive.
        var bytes = await File.ReadAllBytesAsync(dump, cancellationToken);
        await File.WriteAllBytesAsync(dump, bytes[..(bytes.Length / 2)], cancellationToken);
        await SeedKeysAsync(_targetKeys);
        var currentKeys = await KeysOfAsync(_targetKeys);

        var restore = await RunAsync(DeployScripts.Restore, [dump], _target, _targetKeys, cancellationToken);

        Assert.False(restore.Succeeded, "restore accepted a corrupted dump. " + restore.Describe());
        Assert.True(restore.ExitCode > 0, "restore did not run. " + restore.Describe());
        Assert.Equal(currentKeys, await KeysOfAsync(_targetKeys));
        Assert.False(await DatabaseExistsAsync(_target.GetConnectionString(), cancellationToken), "restore created the database for a corrupted dump");
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

        var backup = await RunAsync(DeployScripts.Backup, [], _source, _sourceKeys, cancellationToken);

        Assert.True(backup.Succeeded, "backup failed. " + backup.Describe());
        Assert.True(File.Exists(DumpPathOf(backup)), "the new dump is missing. " + backup.Describe());
        Assert.True(File.Exists(KeysArchiveOf(DumpPathOf(backup))), "the new keys archive is missing. " + backup.Describe());
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

    /// <summary>The keys archive of a dump: <c>apogeu-keys-&lt;stamp&gt;.tar.gz</c> beside <c>apogeu-&lt;stamp&gt;.dump</c>.</summary>
    private static string KeysArchiveOf(string dump)
    {
        var name = Path.GetFileName(dump);
        var stamp = name["apogeu-".Length..].Split('.')[0];
        return Path.Combine(Path.GetDirectoryName(dump) ?? string.Empty, $"apogeu-keys-{stamp}.tar.gz");
    }

    /// <summary>Data Protection key ring of the api in a volume: three XML keys owned by the api user (1654), as the api writes them.</summary>
    private static async Task SeedKeysAsync(string volume)
    {
        await RunCheckedAsync(["volume", "create", volume]).ConfigureAwait(false);
        const string script =
            """
            set -e
            for n in 1 2 3; do
              id="$(head -c 16 /dev/urandom | od -An -tx1 | tr -d ' \n')"
              printf '<?xml version="1.0" encoding="utf-8"?><key id="%s" version="1"><descriptor>%s</descriptor></key>' \
                "$id" "$(head -c 64 /dev/urandom | base64 | tr -d '\n')" > "/keys/key-$id.xml"
            done
            chown -R 1654:1654 /keys
            chmod 600 /keys/*.xml
            """;
        await RunCheckedAsync(["run", "--rm", "--volume", $"{volume}:/keys", "busybox:1.37.0", "sh", "-c", script]).ConfigureAwait(false);
    }

    /// <summary>Every file of a volume: "path sha256 uid:gid mode", sorted.</summary>
    private static async Task<List<string>> KeysOfAsync(string volume)
    {
        var result = await RunCheckedAsync(
            [
                "run", "--rm", "--volume", $"{volume}:/keys:ro", "busybox:1.37.0", "sh", "-c",
                "cd /keys && find . -type f | sort | while read -r f; do echo \"$f $(sha256sum \"$f\" | cut -d' ' -f1) $(stat -c '%u:%g %a' \"$f\")\"; done",
            ]).ConfigureAwait(false);
        return [.. result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static async Task<ProcessResult> RunCheckedAsync(IEnumerable<string> arguments)
    {
        var result = await DockerCli.RunAsync(arguments, CancellationToken.None).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("docker: " + result.Describe());
        }

        return result;
    }

    private Task<ProcessResult> RunAsync(
        string script, IEnumerable<string> arguments, PostgreSqlContainer container, string keysVolume, CancellationToken cancellationToken) =>
        DeployScripts.RunAsync(
            script,
            arguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DeployScripts.DeployDirVariable] = _root,
                [DeployScripts.BackupDirVariable] = BackupDir,
                [DeployScripts.PostgresContainerVariable] = container.Id,
                [DeployScripts.KeysVolumeVariable] = keysVolume,
                [DeployScripts.ProjectVariable] = "apogeu-test-it02",
            },
            cancellationToken);
}
