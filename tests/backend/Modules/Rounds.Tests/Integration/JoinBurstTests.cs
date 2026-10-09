using System.Collections.Concurrent;
using System.Text.Json;
using Apogeu.Rounds.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.TestKit.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Apogeu.Rounds.Tests.Integration;

/// <summary>
/// Regression of the k6 load test of SPEC-0008 §7.6 (IMP-02): a burst of <c>JoinSala</c> must not exhaust the PostgreSQL
/// connections (Npgsql 53300 "sorry, too many clients already").
/// </summary>
[Collection(RoundsIntegration.Name)]
public sealed class JoinBurstTests(RoundsContainers containers)
{
    private const int MaxConnections = 30;
    private const int Clients = 300;
    private const int ConnectParallelism = 25;

    [Fact(DisplayName = "Dado o Host real com PostgreSQL limitado a max_connections 30, quando 300 clientes chamam JoinSala ao mesmo tempo, então todos recebem o snapshot sem erro e o pool nunca esgota o servidor")]
    [Trait("Spec", "SPEC-0008:IT-07")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenPostgresWithLowMaxConnectionsWhenThreeHundredClientsJoinAtOnceThenAllReceiveTheSnapshotWithinThePool()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var postgres = PostgreSqlContainerFixture.WithMaxConnections(MaxConnections);
        await postgres.InitializeAsync();

        // The monitor takes its slot before the Host exists, so it can always observe the server.
        var monitor = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Pooling = false }.ConnectionString);
        await using var monitorScope = monitor;
        await monitor.OpenAsync(ct);
        Assert.Equal(MaxConnections, await ScalarIntAsync(monitor, "SELECT current_setting('max_connections')::int", ct));

        await using var cluster = await RoundsCluster.CreateAsync(containers, postgres.ConnectionString, sala: null, ct);
        using var logs = new LogCapture();
        var host = cluster.StartHost(new RoundsHostSetup
        {
            Salas = new Dictionary<string, SalaSettings>(StringComparer.Ordinal) { [SalaSettings.SalaId] = cluster.Sala },
            Services = services => services.AddSingleton<ILoggerProvider>(logs),
        });
        await Leaders.SingleAsync([host], TimeSpan.FromSeconds(30));

        var connections = new HubConnection[Clients];
        try
        {
            await ConnectAsync(host, connections, ct);

            using var sampling = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var peak = SamplePeakClientBackendsAsync(monitor, sampling.Token);
            var failures = await JoinAllAtOnceAsync(connections, ct);
            await sampling.CancelAsync();
            var observed = await peak;

            var described = string.Join(Environment.NewLine, failures.Take(10));
            Assert.True(
                failures.Count == 0,
                $"{failures.Count} of {Clients} JoinSala failed (max_connections = {MaxConnections}, peak client backends = {observed.Peak}). Server errors: {ServerErrors(logs)}. First failures:{Environment.NewLine}{described}");

            // The Host's pool must leave the server room: reaching max_connections is the "too many clients" of IMP-02.
            Assert.True(
                observed.Peak < MaxConnections,
                $"PostgreSQL reached {observed.Peak} client backends during the burst (max_connections = {MaxConnections}): the Host's pool is not bounded below the server limit.");
        }
        finally
        {
            foreach (var connection in connections.Where(c => c is not null))
            {
                await connection.DisposeAsync();
            }
        }
    }

    private static Task ConnectAsync(RoundsHost host, HubConnection[] connections, CancellationToken ct) =>
        Parallel.ForAsync(0, connections.Length, new ParallelOptions { MaxDegreeOfParallelism = ConnectParallelism, CancellationToken = ct }, async (i, token) =>
        {
            connections[i] = host.CreateHubConnection(player: $"player-{i + 1}");
            await connections[i].StartAsync(token).ConfigureAwait(false);
        });

    /// <summary>Every client calls <c>JoinSala</c> at the same instant; returns the failed joins (errors or a wrong snapshot).</summary>
    private static async Task<IReadOnlyList<string>> JoinAllAtOnceAsync(HubConnection[] connections, CancellationToken ct)
    {
        var failures = new ConcurrentBag<string>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var joins = connections.Select(async (connection, i) =>
        {
            await start.Task.ConfigureAwait(false);
            try
            {
                var snapshot = await connection.InvokeAsync<JsonElement>("JoinSala", SalaSettings.SalaId, ct).ConfigureAwait(false);
                if (!IsSnapshotOfDemo(snapshot))
                {
                    failures.Add($"player-{i + 1}: unexpected snapshot {snapshot.GetRawText()}");
                }
            }
#pragma warning disable CA1031 // Every failed join is collected and reported, whatever its type.
            catch (Exception error)
#pragma warning restore CA1031
            {
                failures.Add($"player-{i + 1}: {error.GetType().Name}: {error.Message}");
            }
        }).ToArray();
        start.SetResult();
        await Task.WhenAll(joins).ConfigureAwait(false);
        return [.. failures];
    }

    /// <summary>The PostgreSQL errors (SQLSTATE and message) logged by the Host, by count.</summary>
    private static string ServerErrors(LogCapture logs)
    {
        var errors = logs.Entries
            .Select(e => FindPostgresException(e.Exception))
            .OfType<PostgresException>()
            .GroupBy(e => $"{e.SqlState} {e.MessageText}", StringComparer.Ordinal)
            .Select(g => $"{g.Count()} x Npgsql {g.Key}")
            .ToList();
        return errors.Count == 0 ? "none from PostgreSQL" : string.Join("; ", errors);
    }

    private static PostgresException? FindPostgresException(Exception? error)
    {
        for (var current = error; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }

    private static bool IsSnapshotOfDemo(JsonElement snapshot) =>
        snapshot.ValueKind == JsonValueKind.Object
        && snapshot.TryGetProperty("sala", out var sala)
        && sala.TryGetProperty("id", out var id)
        && string.Equals(id.GetString(), SalaSettings.SalaId, StringComparison.Ordinal)
        && snapshot.TryGetProperty("history", out var history)
        && history.ValueKind == JsonValueKind.Array;

    private static async Task<PeakSample> SamplePeakClientBackendsAsync(NpgsqlConnection monitor, CancellationToken ct)
    {
        var peak = 0;
        var samples = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var count = await ScalarIntAsync(monitor, "SELECT count(*)::int FROM pg_stat_activity WHERE backend_type = 'client backend'", CancellationToken.None).ConfigureAwait(false);
                peak = Math.Max(peak, count);
                samples++;
                await Task.Delay(5, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // End of the burst.
        }

        return new PeakSample(peak, samples);
    }

    private static async Task<int> ScalarIntAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        var command = new NpgsqlCommand(sql, connection);
        await using (command.ConfigureAwait(false))
        {
            return (int)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
        }
    }

    private sealed record PeakSample(int Peak, int Samples);
}
