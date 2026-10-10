using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Apogeu.Rounds.Tests.Support;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// A player on <c>/hubs/bets</c> through a real SignalR client: invokes <c>PlaceBet</c>, <c>CancelBet</c> and
/// <c>GetBetLimits</c> with contract JSON (money and multipliers as decimal strings, ADR-0008) and records, in order, every
/// result and every <c>BetActivated</c> it receives as raw JSON.
/// </summary>
internal sealed class BetsClient : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly List<HubOutcome> _outcomes = [];
    private readonly List<JsonElement> _activated = [];

    private BetsClient(string player, HubConnection connection)
    {
        Player = player;
        Connection = connection;
        connection.On<JsonElement>("BetActivated", payload =>
        {
            lock (_gate)
            {
                _activated.Add(payload.Clone());
            }
        });
    }

    public string Player { get; }

    public HubConnection Connection { get; }

    public IReadOnlyList<HubOutcome> Outcomes
    {
        get
        {
            lock (_gate)
            {
                return [.. _outcomes];
            }
        }
    }

    public IReadOnlyList<JsonElement> Activated
    {
        get
        {
            lock (_gate)
            {
                return [.. _activated];
            }
        }
    }

    public static async Task<BetsClient> ConnectAsync(BettingHost host, string player, CancellationToken ct, string mode = "demo")
    {
        ArgumentNullException.ThrowIfNull(host);
        var client = new BetsClient(player, host.CreateBetsConnection(player, mode));
        await client.Connection.StartAsync(ct).ConfigureAwait(false);
        return client;
    }

    public Task<HubOutcome> PlaceAsync(Guid clientBetId, int panel, string amount, string? autoCashOut, CancellationToken ct) =>
        InvokeAsync("PlaceBet", new { clientBetId, panel, amount, autoCashOut }, ct);

    public Task<HubOutcome> PlaceAsync(int panel, string amount, CancellationToken ct) => PlaceAsync(Guid.NewGuid(), panel, amount, null, ct);

    public Task<HubOutcome> CancelAsync(Guid betId, CancellationToken ct) => InvokeAsync("CancelBet", new { betId }, ct);

    public async Task<HubOutcome> LimitsAsync(CancellationToken ct)
    {
        HubOutcome outcome;
        try
        {
            var result = await Connection.InvokeAsync<JsonElement>("GetBetLimits", ct).ConfigureAwait(false);
            outcome = new HubOutcome("GetBetLimits", result.Clone(), null);
        }
        catch (HubException exception)
        {
            outcome = new HubOutcome("GetBetLimits", null, exception.Message);
        }

        Record(outcome);
        return outcome;
    }

    /// <summary>Waits for the <c>BetActivated</c> of <paramref name="betId"/>.</summary>
    public async Task<JsonElement> WaitForActivationAsync(Guid betId, TimeSpan timeout)
    {
        var ct = TestContext.Current.CancellationToken;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            var found = Activated.FirstOrDefault(a => a.TryGetProperty("betId", out var id) && id.GetGuid() == betId);
            if (found.ValueKind == JsonValueKind.Object)
            {
                return found;
            }

            await Task.Delay(20, ct).ConfigureAwait(false);
        }

        Assert.Fail($"No BetActivated for {betId} within {timeout}. Received: [{string.Join(", ", Activated.Select(a => a.GetRawText()))}]");
        return default;
    }

    public static Guid BetId(HubOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome.Value.GetProperty("betId").GetGuid();
    }

    public static decimal Money(JsonElement element, string property) => Json.Decimal(element, property);

    public static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private async Task<HubOutcome> InvokeAsync(string method, object argument, CancellationToken ct)
    {
        HubOutcome outcome;
        try
        {
            var result = await Connection.InvokeAsync<JsonElement>(method, argument, ct).ConfigureAwait(false);
            outcome = new HubOutcome(method, result.Clone(), null);
        }
        catch (HubException exception)
        {
            outcome = new HubOutcome(method, null, exception.Message);
        }

        Record(outcome);
        return outcome;
    }

    private void Record(HubOutcome outcome)
    {
        lock (_gate)
        {
            _outcomes.Add(outcome);
        }
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
