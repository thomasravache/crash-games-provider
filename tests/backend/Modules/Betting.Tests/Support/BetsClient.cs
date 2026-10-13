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
/// result and every <c>BetActivated</c> it receives as raw JSON; for SPEC-0023 also <c>CashOut</c> and the server messages
/// <c>CashedOut</c>, <c>CashOutRejected</c> and <c>BalanceUpdated</c>, with their receipt time; for SPEC-0025 also
/// <c>JoinSalaBets</c> and the room's <c>LiveBetsBatch</c> (kept apart in <see cref="LiveBatches"/>).
/// </summary>
internal sealed class BetsClient : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly List<HubOutcome> _outcomes = [];
    private readonly List<JsonElement> _activated = [];
    private readonly List<HubEvent> _events = [];
    private readonly List<HubEvent> _liveBatches = [];

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
        connection.On<JsonElement>(LiveBetsBatchTarget, payload =>
        {
            lock (_gate)
            {
                _liveBatches.Add(new HubEvent(LiveBetsBatchTarget, payload.Clone(), DateTimeOffset.UtcNow));
            }
        });
        foreach (var target in ServerTargets)
        {
            connection.On<JsonElement>(target, payload =>
            {
                lock (_gate)
                {
                    _events.Add(new HubEvent(target, payload.Clone(), DateTimeOffset.UtcNow));
                }
            });
        }
    }

    /// <summary>The server messages of SPEC-0023 and SPEC-0024 recorded by <see cref="Events"/>.</summary>
    public static IReadOnlyList<string> ServerTargets { get; } = ["CashedOut", "CashOutRejected", "BalanceUpdated", "BetSettled", "BetRefunded"];

    /// <summary>Client method of the room's batches of live bets (SPEC-0025 §6).</summary>
    public const string LiveBetsBatchTarget = "LiveBetsBatch";

    public string Player { get; }

    /// <summary>Every <c>LiveBetsBatch</c> received (SPEC-0025), in order, with its receipt time.</summary>
    public IReadOnlyList<HubEvent> LiveBatches
    {
        get
        {
            lock (_gate)
            {
                return [.. _liveBatches];
            }
        }
    }

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

    /// <summary>Every <c>CashedOut</c>, <c>CashOutRejected</c> and <c>BalanceUpdated</c> received, in order.</summary>
    public IReadOnlyList<HubEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    /// <summary>The server messages <paramref name="target"/> received for the bet <paramref name="betId"/>.</summary>
    public IReadOnlyList<HubEvent> EventsOf(string target, Guid betId) =>
        [.. Events.Where(e => string.Equals(e.Target, target, StringComparison.Ordinal)
            && e.Payload.TryGetProperty("betId", out var id) && id.GetGuid() == betId)];

    public string DescribeEvents() => string.Join(Environment.NewLine, Events);

    public static async Task<BetsClient> ConnectAsync(
        BettingHost host,
        string player,
        CancellationToken ct,
        string mode = "demo",
        string? nickname = null,
        string operatorId = BettingSessions.Operator)
    {
        ArgumentNullException.ThrowIfNull(host);
        var client = new BetsClient(player, host.CreateBetsConnection(player, mode, nickname, operatorId));
        await client.Connection.StartAsync(ct).ConfigureAwait(false);
        return client;
    }

    public Task<HubOutcome> PlaceAsync(Guid clientBetId, int panel, string amount, string? autoCashOut, CancellationToken ct) =>
        InvokeAsync("PlaceBet", new { clientBetId, panel, amount, autoCashOut }, ct);

    public Task<HubOutcome> PlaceAsync(int panel, string amount, CancellationToken ct) => PlaceAsync(Guid.NewGuid(), panel, amount, null, ct);

    public Task<HubOutcome> CancelAsync(Guid betId, CancellationToken ct) => InvokeAsync("CancelBet", new { betId }, ct);

    /// <summary><c>CashOut</c> (SPEC-0023 §6): its result is the envelope <c>CashOutResult { cashedOut?, pending? }</c>.</summary>
    public Task<HubOutcome> CashOutAsync(Guid betId, CancellationToken ct) => InvokeAsync("CashOut", new { betId }, ct);

    /// <summary>Waits for a server message <paramref name="target"/> for <paramref name="betId"/> (any, when <paramref name="match"/> is null).</summary>
    public async Task<HubEvent> WaitForEventAsync(string target, Guid? betId, TimeSpan timeout, Func<JsonElement, bool>? match = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            var found = Events.FirstOrDefault(e => string.Equals(e.Target, target, StringComparison.Ordinal)
                && (betId is null || (e.Payload.TryGetProperty("betId", out var id) && id.GetGuid() == betId))
                && (match is null || match(e.Payload)));
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(20, ct).ConfigureAwait(false);
        }

        Assert.Fail($"No {target} for {betId} within {timeout}. Received:{Environment.NewLine}{DescribeEvents()}");
        return null!;
    }

    public Task<HubOutcome> LimitsAsync(CancellationToken ct) => InvokeWithoutArgumentAsync("GetBetLimits", ct);

    /// <summary><c>JoinSalaBets()</c> (SPEC-0025 §6): joins the room's group and answers the <c>LiveBetsSnapshot</c>.</summary>
    public Task<HubOutcome> JoinSalaBetsAsync(CancellationToken ct) => InvokeWithoutArgumentAsync("JoinSalaBets", ct);

    /// <summary>Waits for a <c>LiveBetsBatch</c> received at or after index <paramref name="from"/> that satisfies <paramref name="match"/>.</summary>
    public async Task<HubEvent> WaitForLiveBatchAsync(Func<JsonElement, bool> match, TimeSpan timeout, int from = 0)
    {
        ArgumentNullException.ThrowIfNull(match);
        var ct = TestContext.Current.CancellationToken;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            var found = LiveBatches.Skip(from).FirstOrDefault(e => match(e.Payload));
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(20, ct).ConfigureAwait(false);
        }

        Assert.Fail($"No matching LiveBetsBatch within {timeout}. Received:{Environment.NewLine}{string.Join(Environment.NewLine, LiveBatches)}");
        return null!;
    }

    private async Task<HubOutcome> InvokeWithoutArgumentAsync(string method, CancellationToken ct)
    {
        HubOutcome outcome;
        try
        {
            var result = await Connection.InvokeAsync<JsonElement>(method, ct).ConfigureAwait(false);
            outcome = new HubOutcome(method, result.Clone(), null);
        }
        catch (HubException exception)
        {
            outcome = new HubOutcome(method, null, exception.Message);
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
