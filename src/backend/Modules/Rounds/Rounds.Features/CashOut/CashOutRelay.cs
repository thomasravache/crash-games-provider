using System.Collections.Concurrent;
using System.Text.Json;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Features.Conductor;
using Apogeu.Rounds.Features.Salas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Apogeu.Rounds.Features.CashOut;

/// <summary>
/// Forwards the cash-out requests of an instance that does not lead the room to its leader through Redis pub/sub (SPEC-0023
/// §4): every instance listens on <c>rounds:cashout:{sala}</c>, and the one that leads the room decides with the
/// <see cref="ICashOutArbiter"/> of its own container and answers on the reply channel of the requester. Only the decision
/// travels, never the crash point nor its instant (ADR-0014).
/// </summary>
internal sealed class CashOutRelay(
    RoundsRedis redis,
    SalaCatalog salas,
    LeaderArbiters arbiters,
    IRoundsLeadership leadership,
    IServiceProvider services,
    ILogger<CashOutRelay> logger) : BackgroundService
{
    /// <summary>How long a forwarded request waits for the leader before it fails with <see cref="TimeoutException"/>.</summary>
    public static readonly TimeSpan ForwardTimeout = TimeSpan.FromSeconds(2);

    private const string Decide = "decide";
    private const string Auto = "auto";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _instance = Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<RelayReply>> _waiting = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ISubscriber? _subscriber;

    public async Task<CashOutDecision> DecideAsync(string salaId, RoundId roundId, DateTimeOffset receivedAt, CancellationToken ct)
    {
        var reply = await ForwardAsync(new RelayRequest(Guid.NewGuid(), ReplyChannel, Decide, salaId, roundId.Value, receivedAt, null, null), ct).ConfigureAwait(false);
        return reply.Accepted && reply.Multiplier is { } multiplier ? new CashOutAccepted(new Multiplier(multiplier)) : new CashOutTooLate();
    }

    public async Task ScheduleAutoAsync(string salaId, RoundId roundId, Guid betId, Multiplier target, CancellationToken ct) =>
        await ForwardAsync(new RelayRequest(Guid.NewGuid(), ReplyChannel, Auto, salaId, roundId.Value, null, betId, target.Value), ct).ConfigureAwait(false);

    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SubscribedAsync().ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                RoundsLog.CashOutRelayFailed(logger, "*", exception);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private string ReplyChannel => $"rounds:cashout:reply:{_instance}";

    private static RedisChannel RequestChannel(string salaId) => RedisChannel.Literal($"rounds:cashout:{salaId}");

    private async Task<RelayReply> ForwardAsync(RelayRequest request, CancellationToken ct)
    {
        var subscriber = await SubscribedAsync().ConfigureAwait(false);
        var answer = new TaskCompletionSource<RelayReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting[request.Id] = answer;
        try
        {
            await subscriber.PublishAsync(RequestChannel(request.SalaId), JsonSerializer.Serialize(request, Json)).ConfigureAwait(false);
            return await answer.Task.WaitAsync(ForwardTimeout, ct).ConfigureAwait(false);
        }
        finally
        {
            _waiting.TryRemove(request.Id, out _);
        }
    }

    /// <summary>Subscribes once to the requests of every room and to this instance's replies.</summary>
    private async Task<ISubscriber> SubscribedAsync()
    {
        if (_subscriber is { } subscribed)
        {
            return subscribed;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_subscriber is null)
            {
                var subscriber = await redis.SubscriberAsync().ConfigureAwait(false);
                await subscriber.SubscribeAsync(RedisChannel.Literal(ReplyChannel), (channel, message) => OnReply(message)).ConfigureAwait(false);
                foreach (var sala in salas.All)
                {
                    await subscriber.SubscribeAsync(RequestChannel(sala.Id), (channel, message) => _ = OnRequestAsync(message)).ConfigureAwait(false);
                }

                _subscriber = subscriber;
            }

            return _subscriber;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnReply(RedisValue message)
    {
        if (JsonSerializer.Deserialize<RelayReply>(message.ToString(), Json) is { } reply && _waiting.TryGetValue(reply.Id, out var answer))
        {
            answer.TrySetResult(reply);
        }
    }

    /// <summary>A request forwarded to this room's leader: decided only when this instance leads it with its conductor running.</summary>
    private async Task OnRequestAsync(RedisValue message)
    {
        RelayRequest? request = null;
        try
        {
            request = JsonSerializer.Deserialize<RelayRequest>(message.ToString(), Json);
            if (request is null || !leadership.IsLeading(request.SalaId) || !arbiters.TryGet(request.SalaId, out _))
            {
                return;
            }

            // The arbiter of this instance's own container (decorators included) decides as the leader.
            var arbiter = services.GetRequiredService<ICashOutArbiter>();
            var roundId = new RoundId(request.RoundId);
            RelayReply reply;
            if (string.Equals(request.Kind, Auto, StringComparison.Ordinal))
            {
                await arbiter.ScheduleAutoAsync(request.SalaId, roundId, request.BetId!.Value, new Multiplier(request.Target!.Value), CancellationToken.None).ConfigureAwait(false);
                reply = new RelayReply(request.Id, Accepted: true, Multiplier: null);
            }
            else
            {
                var decision = await arbiter.DecideAsync(request.SalaId, roundId, request.ReceivedAt!.Value, CancellationToken.None).ConfigureAwait(false);
                reply = decision is CashOutAccepted accepted
                    ? new RelayReply(request.Id, Accepted: true, accepted.Multiplier.Value)
                    : new RelayReply(request.Id, Accepted: false, Multiplier: null);
            }

            var subscriber = await SubscribedAsync().ConfigureAwait(false);
            await subscriber.PublishAsync(RedisChannel.Literal(request.ReplyTo), JsonSerializer.Serialize(reply, Json)).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A request that cannot be answered times out on the requester, which decides it later (CashOutPending).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            RoundsLog.CashOutRelayFailed(logger, request?.SalaId ?? "?", exception);
        }
    }

    /// <summary>A request forwarded to the leader: a manual decision (<c>receivedAt</c>) or an automatic cash-out to schedule.</summary>
    private sealed record RelayRequest(Guid Id, string ReplyTo, string Kind, string SalaId, Guid RoundId, DateTimeOffset? ReceivedAt, Guid? BetId, decimal? Target);

    /// <summary>The leader's answer: only the decision (and the multiplier of an accepted cash-out).</summary>
    private sealed record RelayReply(Guid Id, bool Accepted, decimal? Multiplier);
}
