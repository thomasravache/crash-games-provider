using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>Records, in order and as raw JSON, every round message a SignalR client receives (SPEC-0008 §6).</summary>
internal sealed class HubRecorder
{
    public static readonly IReadOnlyList<string> Targets = ["RoundBetting", "RoundFlying", "RoundSync", "RoundCrashed", "RoundAnnulled"];

    private readonly Lock _gate = new();
    private readonly List<HubEvent> _events = [];

    public HubRecorder(HubConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        foreach (var target in Targets)
        {
            connection.On<JsonElement>(target, payload => Record(target, payload));
        }
    }

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

    public IReadOnlyList<HubEvent> Of(string target, Guid? roundId = null) =>
        [.. Events.Where(e => string.Equals(e.Target, target, StringComparison.Ordinal) && (roundId is null || e.RoundId == roundId))];

    public string Describe() => string.Join(Environment.NewLine, Events);

    /// <summary>Waits for the first message, received from index <paramref name="from"/> on, that matches.</summary>
    public async Task<HubEvent> WaitForAsync(string target, Func<HubEvent, bool> match, TimeSpan timeout, int from = 0)
    {
        var ct = TestContext.Current.CancellationToken;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            var found = Events.Skip(from).FirstOrDefault(e => string.Equals(e.Target, target, StringComparison.Ordinal) && match(e));
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(20, ct).ConfigureAwait(false);
        }

        Assert.Fail($"No {target} within {timeout}. Received:{Environment.NewLine}{Describe()}");
        return null!;
    }

    public Task<HubEvent> WaitForAsync(string target, TimeSpan timeout, int from = 0) => WaitForAsync(target, _ => true, timeout, from);

    private void Record(string target, JsonElement payload)
    {
        lock (_gate)
        {
            _events.Add(new HubEvent(target, payload.Clone(), DateTimeOffset.UtcNow));
        }
    }
}
