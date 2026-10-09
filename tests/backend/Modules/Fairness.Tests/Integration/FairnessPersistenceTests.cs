using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.TestKit.Logging;
using Npgsql;
using Xunit;

namespace Apogeu.Fairness.Tests.Integration;

[Collection(FairnessPostgres.Name)]
public sealed partial class FairnessPersistenceTests(PostgreSqlContainerFixture postgres)
{
    private static readonly Rtp DemoRtp = new(0.97m);
    private static readonly Multiplier DefaultCap = new(10000.00m);

    [Fact(DisplayName = "Dado PostgreSQL real, quando uma cadeia de 50.000 elos é criada, então o banco contém só a origem cifrada e 5 checkpoints, e RevealAsync da rodada 12.345 devolve a semente que encadeia com a 12.344")]
    [Trait("Spec", "SPEC-0007:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealPostgresWhenChainIsCreatedThenOnlyEncryptedOriginAndCheckpointsAreStoredAndRevealChains()
    {
        const int Length = 50_000;
        const int Round = 12_345;
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        using var keys = new TemporaryKeyDirectory();
        await using var host = await FairnessTestHost.StartAsync(connectionString, keys.Directory, logCapture: null, ct);
        var sala = new SalaId(Guid.NewGuid());

        var chain = await host.RunAsync(fairness => fairness.CreateChainAsync(sala, Length, ct));
        var stored = await StoredStateAsync(connectionString, ct);

        Assert.Equal(Length, chain.Length);
        Assert.True(stored.Tables.Count > 0, "The schema 'fairness' has no table: the chain was not persisted in PostgreSQL.");
        Assert.True(stored.Rows <= 10, $"Expected only the chain and its 5 checkpoints, found {stored.Rows} rows in the schema 'fairness'.");
        Assert.True(stored.Bytes <= 16 * 1024, $"Expected only the origin and 5 checkpoints, found {stored.Bytes} bytes in the schema 'fairness'.");
        Assert.InRange(stored.EncryptedPayloads, 5, 6);

        var commitment = await AdvanceAsync(host, sala, Round, ct);

        Assert.Equal(chain.ChainId, commitment.ChainId);
        Assert.Equal(Round, commitment.RoundIndex);

        var revealed = await host.RunAsync(fairness => fairness.RevealAsync(chain.ChainId, Round, DemoRtp, DefaultCap, ct));
        var previous = await host.RunAsync(fairness => fairness.RevealAsync(chain.ChainId, Round - 1, DemoRtp, DefaultCap, ct));
        var seed = revealed.Seed.ToArray();

        Assert.Equal(SHA256.HashData(seed), revealed.PreviousSeed.ToArray());
        Assert.Equal(previous.Seed.ToArray(), revealed.PreviousSeed.ToArray());
        Assert.Equal(commitment.Commitment.ToArray(), revealed.PreviousSeed.ToArray());
        Assert.Equal(chain.Salt.ToArray(), revealed.Salt.ToArray());

        // s_r = h_{N-r}: hashing it r times reaches the published terminal hash h_N.
        var link = seed;
        for (var r = 0; r < Round; r++)
        {
            link = SHA256.HashData(link);
        }

        Assert.Equal(chain.TerminalHash.ToArray(), link);

        var expectedCrash = ReferenceAlgorithm.CrashPoint(seed, chain.Salt.ToArray(), DemoRtp.Value, DefaultCap.Value);
        Assert.Equal(expectedCrash, revealed.CrashPoint.Value);
        Assert.Equal(
            VerificationResult.Valid,
            host.Run(fairness => fairness.Verify(revealed.Seed, revealed.PreviousSeed, revealed.Salt, DemoRtp, DefaultCap, revealed.CrashPoint)));

        // At creation every link but h_N was unrevealed; the links now computable from s_12345 (h_37655..h_49999, the
        // checkpoint h_40000 included) must not have been stored in clear text.
        AssertNotInClearText(stored.Dump, seed, firstLink: Length - Round, lastLink: Length - 1);
    }

    [Fact(DisplayName = "Dado um processo que avança 3 rodadas e é reiniciado, quando NextRoundAsync é chamado, então o índice é 4 e a semente obtida ≤ 20 ms")]
    [Trait("Spec", "SPEC-0007:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenProcessRestartedAfterThreeRoundsWhenNextRoundThenIndexIsFourWithinTwentyMilliseconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        using var keys = new TemporaryKeyDirectory();
        var sala = new SalaId(Guid.NewGuid());
        ChainInfo chain;
        RoundSeedCommitment third;

        await using (var first = await FairnessTestHost.StartAsync(connectionString, keys.Directory, logCapture: null, ct))
        {
            // 50,000 links: round 4 uses h_49996, up to 9,996 hashes away from the nearest checkpoint.
            chain = await first.RunAsync(fairness => fairness.CreateChainAsync(sala, 50_000, ct));
            for (var r = 1; r <= 2; r++)
            {
                await first.RunAsync(fairness => fairness.NextRoundAsync(sala, ct));
            }

            third = await first.RunAsync(fairness => fairness.NextRoundAsync(sala, ct));
        }

        Assert.Equal(3, third.RoundIndex);

        await using var restarted = await FairnessTestHost.StartAsync(connectionString, keys.Directory, logCapture: null, ct);

        // Warm-up of the new process on another room, so the measurement is the seed retrieval and not the first-call JIT.
        var otherSala = new SalaId(Guid.NewGuid());
        await restarted.RunAsync(fairness => fairness.CreateChainAsync(otherSala, 20_000, ct));
        await restarted.RunAsync(fairness => fairness.NextRoundAsync(otherSala, ct));

        var stopwatch = Stopwatch.StartNew();
        var fourth = await restarted.RunAsync(fairness => fairness.NextRoundAsync(sala, ct));
        stopwatch.Stop();

        Assert.Equal(chain.ChainId, fourth.ChainId);
        Assert.Equal(4, fourth.RoundIndex);
        Assert.True(stopwatch.Elapsed <= TimeSpan.FromMilliseconds(20), $"Next round after restart took {stopwatch.Elapsed.TotalMilliseconds:F1} ms, above 20 ms.");

        // Neither repeated nor skipped: the commitment of round 4 is SHA256(s_4) = s_3, the seed of round 3.
        var revealedThird = await restarted.RunAsync(fairness => fairness.RevealAsync(chain.ChainId, 3, DemoRtp, DefaultCap, ct));
        var revealedFourth = await restarted.RunAsync(fairness => fairness.RevealAsync(chain.ChainId, 4, DemoRtp, DefaultCap, ct));
        Assert.Equal(revealedThird.Seed.ToArray(), fourth.Commitment.ToArray());
        Assert.Equal(SHA256.HashData(revealedFourth.Seed.ToArray()), fourth.Commitment.ToArray());
        Assert.NotEqual(third.Commitment.ToArray(), fourth.Commitment.ToArray());
    }

    [Fact(DisplayName = "Dada a captura de logs ativa, quando a cadeia é criada, avançada e revelada, então nenhuma semente não revelada aparece em log")]
    [Trait("Spec", "SPEC-0007:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenLogCaptureWhenChainIsCreatedAdvancedAndRevealedThenNoUnrevealedSeedIsLogged()
    {
        const int Length = 20;
        const int Played = 3;
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        using var keys = new TemporaryKeyDirectory();
        var capture = new LogCapture();
        await using var host = await FairnessTestHost.StartAsync(connectionString, keys.Directory, capture, ct);
        var sala = new SalaId(Guid.NewGuid());

        var chain = await host.RunAsync(fairness => fairness.CreateChainAsync(sala, Length, ct));
        for (var r = 1; r <= Played; r++)
        {
            await host.RunAsync(fairness => fairness.NextRoundAsync(sala, ct));
        }

        await host.RunAsync(fairness => fairness.RevealAsync(chain.ChainId, Played, DemoRtp, DefaultCap, ct));
        var logged = capture.Entries
            .Select(entry => string.Join('\n', entry.Category, entry.Message, entry.Exception?.ToString() ?? string.Empty))
            .ToList();

        Assert.True(logged.Count > 0, "The log capture saw no entry: the test would prove nothing.");

        // Only now, outside the captured window, play the whole chain to learn h_0 and every seed that was unrevealed.
        for (var r = Played + 1; r <= Length; r++)
        {
            await host.RunAsync(fairness => fairness.NextRoundAsync(sala, ct));
        }

        var last = await host.RunAsync(fairness => fairness.RevealAsync(chain.ChainId, Length, DemoRtp, DefaultCap, ct));
        var links = ReferenceAlgorithm.Chain(last.Seed.ToArray(), Length);
        Assert.Equal(chain.TerminalHash.ToArray(), links[Length]);

        // Unrevealed during the window: s_4..s_N = h_{N-4}..h_0.
        var unrevealed = Enumerable.Range(0, Length - Played).Select(k => links[k]).ToList();
        var leaks = logged
            .Where(text => unrevealed.SelectMany(SeedFormats.Of).Any(form => text.Contains(form, StringComparison.Ordinal)))
            .ToList();

        Assert.True(leaks.Count == 0, "Unrevealed seeds found in logs:" + Environment.NewLine + string.Join(Environment.NewLine, leaks));
    }

    [Fact(DisplayName = "Dada uma cadeia de 3 elos já consumida, quando NextRoundAsync, então lança ChainExhausted")]
    [Trait("Spec", "SPEC-0007:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenConsumedChainOfThreeWhenNextRoundThenChainExhausted()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        using var keys = new TemporaryKeyDirectory();
        await using var host = await FairnessTestHost.StartAsync(connectionString, keys.Directory, logCapture: null, ct);
        var sala = new SalaId(Guid.NewGuid());

        await host.RunAsync(fairness => fairness.CreateChainAsync(sala, 3, ct));
        var indexes = new List<int>();
        for (var r = 1; r <= 3; r++)
        {
            indexes.Add((await host.RunAsync(fairness => fairness.NextRoundAsync(sala, ct))).RoundIndex);
        }

        Assert.Equal([1, 2, 3], indexes);
        await Assert.ThrowsAsync<ChainExhaustedException>(() => host.RunAsync(fairness => fairness.NextRoundAsync(sala, ct)));
    }

    private static async Task<RoundSeedCommitment> AdvanceAsync(FairnessTestHost host, SalaId sala, int rounds, CancellationToken ct)
    {
        RoundSeedCommitment? last = null;
        for (var r = 1; r <= rounds; r++)
        {
            last = await host.RunAsync(fairness => fairness.NextRoundAsync(sala, ct)).ConfigureAwait(false);
        }

        return last!;
    }

    /// <summary>Fails when any link h_first..h_last (h_first given, the next ones hashed from it) appears in the dump.</summary>
    private static void AssertNotInClearText(string dump, byte[] firstLinkBytes, int firstLink, int lastLink)
    {
        var leaked = new List<int>();
        var link = firstLinkBytes;
        for (var k = firstLink; k <= lastLink; k++)
        {
            if (k > firstLink)
            {
                link = SHA256.HashData(link);
            }

            if (SeedFormats.Of(link).Any(form => dump.Contains(form, StringComparison.Ordinal)))
            {
                leaked.Add(k);
            }
        }

        Assert.True(leaked.Count == 0, "Chain links stored in clear text in the schema 'fairness': h_" + string.Join(", h_", leaked));
    }

    /// <summary>What the schema <c>fairness</c> holds: its tables, rows, bytes, Data Protection payloads and a text dump.</summary>
    private static async Task<StoredState> StoredStateAsync(string connectionString, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            var tables = await QueryAsync(
                connection,
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'fairness' AND table_type = 'BASE TABLE' "
                + "AND table_name <> '__EFMigrationsHistory' ORDER BY table_name",
                reader => reader.GetString(0),
                ct).ConfigureAwait(false);

            long bytes = 0;
            var rows = new List<string>();
            foreach (var table in tables)
            {
                var qualified = "fairness.\"" + table.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
                var tableRows = await QueryAsync(
                    connection,
                    $"SELECT row_to_json(t)::text, pg_column_size(t.*) FROM {qualified} t",
                    reader => (Json: reader.GetString(0), Size: reader.GetInt32(1)),
                    ct).ConfigureAwait(false);
                rows.AddRange(tableRows.Select(row => row.Json));
                bytes += tableRows.Sum(row => (long)row.Size);
            }

            var dump = string.Join(Environment.NewLine, rows);
            return new StoredState(tables, rows.Count, bytes, DataProtectionPayload().Count(dump), dump);
        }
    }

    private static async Task<List<T>> QueryAsync<T>(NpgsqlConnection connection, string sql, Func<NpgsqlDataReader, T> read, CancellationToken ct)
    {
        var values = new List<T>();
        var command = new NpgsqlCommand(sql, connection);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    values.Add(read(reader));
                }
            }
        }

        return values;
    }

    /// <summary>The magic header of ASP.NET Core Data Protection payloads (0x09F0C9F0), as bytea hex or as base64 text.</summary>
    [GeneratedRegex(@"\\\\x09f0c9f0|CfDJ8", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DataProtectionPayload();

    private sealed record StoredState(IReadOnlyList<string> Tables, long Rows, long Bytes, int EncryptedPayloads, string Dump);
}
