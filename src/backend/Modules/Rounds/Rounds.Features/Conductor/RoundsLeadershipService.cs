using System.Collections.Concurrent;
using System.Globalization;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.CashOut;
using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Features.Salas;
using Apogeu.Rounds.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Apogeu.Rounds.Features.Conductor;

/// <summary>
/// One leader per room (ADR-0005): each instance tries to take the lock <c>rounds:leader:{sala}</c> (SET NX, TTL 5 s) and,
/// while it holds it, renews it every second and runs the room's <see cref="SalaConductor"/>. Losing the lock (failed
/// renewal) stops the conductor; stopping the host releases the lock so another instance takes over at once. The term is
/// bounded by the lease itself: a renewal that does not answer in time (a hung connection, a paused process) does not
/// extend it, and the conductor stops <see cref="RenewInterval"/> before the lock can expire, so the old leader never
/// conducts once another instance may hold the lock. Each renewal
/// also records when the leader was last seen alive (<c>rounds:leader-seen:{sala}</c>), so the next leader knows how long
/// an interrupted round stayed without one.
/// </summary>
internal sealed class RoundsLeadershipService(
    SalaCatalog salas,
    RoundsRedis redis,
    IServiceScopeFactory scopes,
    IRoundBroadcaster broadcaster,
    SalaLiveViews views,
    RoundsMetrics metrics,
    LeaderArbiters arbiters,
    IAutoCashOutSink autoCashOuts,
    TimeProvider time,
    ILoggerFactory loggers) : BackgroundService, IRoundsLeadership
{
    public static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(1);

    /// <summary>How often a follower tries to take the lock.</summary>
    public static readonly TimeSpan AcquireInterval = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan LastSeenTtl = TimeSpan.FromDays(1);

    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromSeconds(1);

    /// <summary>KEYS: lock, last seen. ARGV: owner, lock TTL ms, now (unix ms), last seen TTL ms. 1 when still the owner.</summary>
    private static readonly string RenewScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
          redis.call('pexpire', KEYS[1], ARGV[2])
          redis.call('set', KEYS[2], ARGV[3], 'PX', ARGV[4])
          return 1
        end
        return 0
        """;

    /// <summary>KEYS: lock, last seen. ARGV: owner, now (unix ms), last seen TTL ms. Deletes the lock only if still the owner.</summary>
    private static readonly string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
          redis.call('set', KEYS[2], ARGV[2], 'PX', ARGV[3])
          return redis.call('del', KEYS[1])
        end
        return 0
        """;

    private readonly string _owner = Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<string, bool> _leading = new(StringComparer.Ordinal);
    private readonly ILogger _logger = loggers.CreateLogger<RoundsLeadershipService>();

    public static RedisKey LockKey(string salaId) => $"rounds:leader:{salaId}";

    public static RedisKey LastSeenKey(string salaId) => $"rounds:leader-seen:{salaId}";

    public bool IsLeading(string salaId) => _leading.TryGetValue(salaId, out var leading) && leading;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(salas.All.Select(sala => LeadAsync(sala, stoppingToken)));

    private async Task LeadAsync(SalaConfig sala, CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            IDatabase db;
            DateTimeOffset? lastSeen;
            DateTimeOffset requestedAt;
            try
            {
                db = await redis.DatabaseAsync().ConfigureAwait(false);
                lastSeen = await ReadLastSeenAsync(db, sala.Id).ConfigureAwait(false);

                // The lease runs from when the lock was requested at the latest (Redis starts its TTL after this instant).
                requestedAt = time.GetUtcNow();
                if (!await db.StringSetAsync(LockKey(sala.Id), _owner, LockTtl, When.NotExists).ConfigureAwait(false))
                {
                    await DelayAsync(AcquireInterval, stopping).ConfigureAwait(false);
                    continue;
                }
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                RoundsLog.LeadershipUnavailable(_logger, sala.Id, exception);
                await DelayAsync(RetryAfterFailure, stopping).ConfigureAwait(false);
                continue;
            }

            if (time.GetUtcNow() >= LeaseEnd(requestedAt))
            {
                // The answer came too late to lead safely: let the lock expire and try again.
                continue;
            }

            await LeadTermAsync(sala, db, lastSeen, requestedAt, stopping).ConfigureAwait(false);
        }
    }

    private async Task LeadTermAsync(SalaConfig sala, IDatabase db, DateTimeOffset? lastSeen, DateTimeOffset acquiredAt, CancellationToken stopping)
    {
        _leading[sala.Id] = true;
        metrics.LeaderChanged(sala.Id);
        RoundsLog.LeadershipAcquired(_logger, sala.Id);

        using var term = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        var lease = new LeaderLease(LeaseEnd(acquiredAt), time, term);
        var renewing = RenewAsync(sala.Id, db, lease, term.Token);
        var conducting = ConductAsync(sala, lastSeen, lease, term.Token);

        // A plain array: a params span would make the compiler emit a global-namespace inline-array helper type.
        Task[] tasks = [renewing, conducting];
        await Task.WhenAny(tasks).ConfigureAwait(false);

        // Lost the lock (or the host stops): stop conducting before anything else.
        _leading[sala.Id] = false;
        await term.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (stopping.IsCancellationRequested)
        {
            await ReleaseAsync(sala.Id, db).ConfigureAwait(false);
        }
        else
        {
            RoundsLog.LeadershipLost(_logger, sala.Id);
        }
    }

    /// <summary>
    /// Renews the lock every second; returns when it is lost, when the lease runs out without a renewal answering in time,
    /// or when the term ends.
    /// </summary>
    private async Task RenewAsync(string salaId, IDatabase db, LeaderLease lease, CancellationToken term)
    {
        while (!term.IsCancellationRequested)
        {
            if (!await DelayAsync(RenewInterval, term).ConfigureAwait(false))
            {
                return;
            }

            var now = time.GetUtcNow();
            var remaining = lease.End - now;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            try
            {
                // A hung call must not keep this instance leading past its lease: wait for it only while the lease lasts.
                var result = await db.ScriptEvaluateAsync(
                    RenewScript,
                    [LockKey(salaId), LastSeenKey(salaId)],
                    [_owner, (long)LockTtl.TotalMilliseconds, now.ToUnixTimeMilliseconds(), (long)LastSeenTtl.TotalMilliseconds])
                    .WaitAsync(remaining, time, term)
                    .ConfigureAwait(false);
                if ((long)result != 1)
                {
                    return;
                }

                lease.Extend(LeaseEnd(now));
            }
            catch (OperationCanceledException) when (term.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                RoundsLog.LeadershipUnavailable(_logger, salaId, exception);

                // Without a renewal the lock expires: stop before another instance can take it.
                if (time.GetUtcNow() >= lease.End)
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// When this instance must stop leading after a lock set or renewed at <paramref name="renewedAt"/>: one renewal interval
    /// before its TTL can expire in Redis.
    /// </summary>
    private static DateTimeOffset LeaseEnd(DateTimeOffset renewedAt) => renewedAt + LockTtl - RenewInterval;

    /// <summary>Runs the conductor of the room until the term ends; a failed conductor resumes from the store.</summary>
    private async Task ConductAsync(SalaConfig sala, DateTimeOffset? lastSeen, LeaderLease lease, CancellationToken term)
    {
        // Every message of the term is fenced by its lease.
        var fenced = lease.Fence(broadcaster);
        while (!term.IsCancellationRequested)
        {
            // The state this conductor publishes is served to the joins only while it runs and the lease holds.
            var snapshots = views.Open(sala.Id, lease);
            var scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            using (snapshots)
            {
                var conductor = CreateConductor(sala, scope.ServiceProvider, fenced, lastSeen, snapshots);

                // The cash-outs of the room are decided here while this conductor runs (SPEC-0023).
                using var arbiter = arbiters.Lead(sala.Id, new LeaderCashOutArbiter(conductor, autoCashOuts));
                try
                {
                    while (true)
                    {
                        lease.Ensure();
                        var woken = conductor.Woken;
                        var next = await conductor.StepAsync(term).ConfigureAwait(false);
                        var wait = next - time.GetUtcNow();
                        if (wait > TimeSpan.Zero && !await DelayAsync(wait, woken, term).ConfigureAwait(false))
                        {
                            return;
                        }

                        term.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException) when (term.IsCancellationRequested)
                {
                    return;
                }
#pragma warning disable CA1031 // The leader must keep the room running whatever failed; the failure is logged and the conductor resumes.
                catch (Exception exception)
#pragma warning restore CA1031
                {
                    RoundsLog.ConductorFailed(_logger, sala.Id, exception);

                    // This instance was alive (and leading) until now.
                    lastSeen = time.GetUtcNow();
                }
            }

            if (!await DelayAsync(RetryAfterFailure, term).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private SalaConductor CreateConductor(
        SalaConfig sala,
        IServiceProvider services,
        IRoundBroadcaster fenced,
        DateTimeOffset? lastSeen,
        ISalaSnapshotPublisher snapshots) =>
        new(
            sala,
            services.GetRequiredService<IFairnessService>(),
            services.GetRequiredService<IRoundStore>(),
            fenced,
            metrics,
            time,
            loggers.CreateLogger<SalaConductor>(),
            services.GetRequiredService<IRoundChainStore>(),
            lastSeen,
            snapshots,
            services.GetRequiredService<IRoundOutbox>());

    private async Task ReleaseAsync(string salaId, IDatabase db)
    {
        try
        {
            await db.ScriptEvaluateAsync(
                ReleaseScript,
                [LockKey(salaId), LastSeenKey(salaId)],
                [_owner, time.GetUtcNow().ToUnixTimeMilliseconds(), (long)LastSeenTtl.TotalMilliseconds])
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            // The lock expires by itself within its TTL.
            RoundsLog.LeadershipUnavailable(_logger, salaId, exception);
        }
    }

    private static async Task<DateTimeOffset?> ReadLastSeenAsync(IDatabase db, string salaId)
    {
        var value = await db.StringGetAsync(LastSeenKey(salaId)).ConfigureAwait(false);
        return value.HasValue && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : null;
    }

    /// <summary>
    /// Waits on the server clock until <paramref name="delay"/> passes or <paramref name="woken"/> completes (an automatic
    /// cash-out was scheduled earlier than the next action); <see langword="false"/> when cancelled.
    /// </summary>
    private async Task<bool> DelayAsync(TimeSpan delay, Task woken, CancellationToken ct)
    {
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var delaying = Task.Delay(delay, time, waiting.Token);
        await Task.WhenAny(delaying, woken).ConfigureAwait(false);
        await waiting.CancelAsync().ConfigureAwait(false);
        return !ct.IsCancellationRequested;
    }

    /// <summary>Waits on the server clock; <see langword="false"/> when cancelled.</summary>
    private async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, time, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
