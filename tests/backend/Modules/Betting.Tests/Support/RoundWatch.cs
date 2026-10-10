using System.Text.Json;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Rounds.Tests.Support;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Follows the phases of the room <c>demo</c> on <c>/hubs/rounds</c> (SPEC-0008) and waits until Betting has received the
/// same phase through the Rounds integration events (<see cref="IRoundFeed"/>), so a bet is sent in a known phase.
/// </summary>
internal sealed class RoundWatch : IAsyncDisposable
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FeedTimeout = TimeSpan.FromSeconds(10);

    private readonly BettingHost _host;
    private readonly HubConnection _connection;

    private RoundWatch(BettingHost host, HubConnection connection)
    {
        _host = host;
        _connection = connection;
        Recorder = new HubRecorder(connection);
    }

    public HubRecorder Recorder { get; }

    public static async Task<RoundWatch> StartAsync(BettingHost host, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        var watch = new RoundWatch(host, host.CreateRoundsConnection());
        await watch._connection.StartAsync(ct).ConfigureAwait(false);
        await watch._connection.InvokeAsync<JsonElement>("JoinSala", BettingSala.SalaId, ct).ConfigureAwait(false);
        return watch;
    }

    /// <summary>
    /// Waits for a round whose bets opened after this call and still have at least <paramref name="remaining"/> on the clock,
    /// and until Betting sees it in the betting phase. Returns its <c>RoundBetting</c> message.
    /// </summary>
    public async Task<HubEvent> NextBettingAsync(TimeSpan remaining)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var betting = await Recorder.WaitForAsync("RoundBetting", RoundTimeout, Recorder.Events.Count).ConfigureAwait(false);
            await FeedAsync(betting.RoundId, RoundPhase.Betting).ConfigureAwait(false);
            if (Json.Instant(betting.Payload, "bettingEndsAt") - DateTimeOffset.UtcNow >= remaining)
            {
                return betting;
            }
        }

        throw new Xunit.Sdk.XunitException($"No round with {remaining} of bets left. Received:{Environment.NewLine}{Recorder.Describe()}");
    }

    /// <summary>Waits for a flight that starts after this call and until Betting sees it. Returns its <c>RoundFlying</c> message.</summary>
    public async Task<HubEvent> NextFlightAsync()
    {
        var flying = await Recorder.WaitForAsync("RoundFlying", RoundTimeout, Recorder.Events.Count).ConfigureAwait(false);
        await FeedAsync(flying.RoundId, RoundPhase.Flying).ConfigureAwait(false);
        return flying;
    }

    /// <summary>The <c>RoundBetting</c> of <paramref name="roundId"/>.</summary>
    public HubEvent BettingOf(Guid roundId) => Recorder.Of("RoundBetting", roundId).Single();

    private Task<TimeSpan> FeedAsync(Guid roundId, RoundPhase phase)
    {
        var feed = _host.Services.GetRequiredService<IRoundFeed>();
        return Eventually.HoldsAsync(
            () => feed.Current(BettingSala.SalaId) is { } current && current.RoundId.Value == roundId && current.Phase >= phase,
            FeedTimeout,
            () => $"Betting sees round {roundId} in {phase} (it sees {feed.Current(BettingSala.SalaId)})");
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
