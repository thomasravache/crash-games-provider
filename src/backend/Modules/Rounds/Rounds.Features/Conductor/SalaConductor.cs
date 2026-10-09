using System.Diagnostics;
using System.Globalization;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Features.Salas;
using Microsoft.Extensions.Logging;
using ContractMultiplier = Apogeu.BuildingBlocks.Contracts.Multiplier;
using FairnessMultiplier = Apogeu.Fairness.Contracts.Multiplier;

namespace Apogeu.Rounds.Features.Conductor;

/// <summary>
/// Conducts the rounds of one room while this instance leads it (SPEC-0008 §4): Betting → Flying → Crashed → new Betting,
/// on the server clock (<see cref="TimeProvider"/>). At the start of the flight it gets the crash point with
/// <c>GetCrashPointAsync</c> (SPEC-0007@2) and keeps it in memory only; <c>RevealAsync</c> is called only after the crash.
/// On its first step it resumes the latest round of the store (same <c>startedAt</c>; beyond 30 s without a leader →
/// Annulled).
/// </summary>
public sealed class SalaConductor
{
    /// <summary>Interval of <c>RoundSync</c> during the flight; syncs fall on <c>startedAt + n·SyncInterval</c>.</summary>
    public static readonly TimeSpan SyncInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>Deadline to resume an interrupted round before it is annulled (ADR-0018).</summary>
    public static readonly TimeSpan ResumeDeadline = TimeSpan.FromSeconds(30);

    /// <summary>Reason recorded on a round annulled because no leader resumed it within <see cref="ResumeDeadline"/>.</summary>
    public const string ResumeDeadlineReason = "RESUME_DEADLINE_EXCEEDED";

    /// <summary>Upper bound of actions run by one step, so a step always returns even if the clock jumps far ahead.</summary>
    private const int MaxActionsPerStep = 64;

    private static readonly ActivitySource Activities = new(RoundsMetrics.MeterName);

    private readonly SalaConfig _sala;
    private readonly IFairnessService _fairness;
    private readonly IRoundStore _store;
    private readonly IRoundBroadcaster _broadcaster;
    private readonly RoundsMetrics _metrics;
    private readonly TimeProvider _time;
    private readonly ILogger<SalaConductor> _logger;
    private readonly IRoundChainStore? _chains;
    private readonly DateTimeOffset? _leaderLastSeenAt;
    private readonly GrowthCurve _curve;
    private readonly Rtp _rtp;
    private readonly FairnessMultiplier _cap;
    private readonly SalaId _fairnessSala;
    private readonly string _growthK;

    private bool _resumed;
    private Round? _round;
    private long _nextNumber = 1;
    private DateTimeOffset _nextRoundAt;

    // The crash point of the round in flight: in memory only, never persisted, logged or sent before the crash.
    private decimal? _crashPoint;
    private DateTimeOffset _crashAt;
    private long _nextSync;

    public SalaConductor(
        SalaConfig sala,
        IFairnessService fairness,
        IRoundStore store,
        IRoundBroadcaster broadcaster,
        RoundsMetrics metrics,
        TimeProvider time,
        ILogger<SalaConductor> logger)
        : this(sala, fairness, store, broadcaster, metrics, time, logger, chains: null, leaderLastSeenAt: null)
    {
    }

    /// <param name="sala">The room conducted.</param>
    /// <param name="fairness">SPEC-0007@2: commitment, crash point and reveal of each round.</param>
    /// <param name="store">The rounds of the room (source of truth for resuming).</param>
    /// <param name="broadcaster">The room group of the hub.</param>
    /// <param name="metrics">Business metrics of the module.</param>
    /// <param name="time">The server clock.</param>
    /// <param name="logger">Logger (ids only, never a crash point or seed before the crash).</param>
    /// <param name="chains">Records the public data of every chain created, for the verification of its rounds.</param>
    /// <param name="leaderLastSeenAt">
    /// The last instant the previous leader of the room was seen alive, when known: an interrupted round is annulled when
    /// it stayed without a leader beyond <see cref="ResumeDeadline"/>.
    /// </param>
    public SalaConductor(
        SalaConfig sala,
        IFairnessService fairness,
        IRoundStore store,
        IRoundBroadcaster broadcaster,
        RoundsMetrics metrics,
        TimeProvider time,
        ILogger<SalaConductor> logger,
        IRoundChainStore? chains,
        DateTimeOffset? leaderLastSeenAt)
    {
        ArgumentNullException.ThrowIfNull(sala);
        ArgumentNullException.ThrowIfNull(fairness);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(broadcaster);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _sala = sala;
        _fairness = fairness;
        _store = store;
        _broadcaster = broadcaster;
        _metrics = metrics;
        _time = time;
        _logger = logger;
        _chains = chains;
        _leaderLastSeenAt = leaderLastSeenAt;
        _curve = new GrowthCurve(sala.GrowthK);
        _rtp = new Rtp(sala.Rtp);
        _cap = new FairnessMultiplier(sala.Cap);
        _fairnessSala = SalaIdentity.ToFairness(sala.Id);
        _growthK = FormatGrowthK(sala.GrowthK);
    }

    /// <summary>The contract text of a growth constant (<c>"0.00006"</c>, SPEC-0008 §6).</summary>
    public static string FormatGrowthK(double growthK) => ((decimal)growthK).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Runs every action due at the current time of the <see cref="TimeProvider"/> (open bets, start the flight, sync,
    /// crash, annul) and returns the instant of the next action. The leader loop waits until that instant and calls it again.
    /// </summary>
    public async Task<DateTimeOffset> StepAsync(CancellationToken ct)
    {
        if (!_resumed)
        {
            await ResumeAsync(ct).ConfigureAwait(false);
            _resumed = true;
        }

        for (var action = 0; action < MaxActionsPerStep; action++)
        {
            var next = await RunDueAsync(_time.GetUtcNow(), ct).ConfigureAwait(false);
            if (next > _time.GetUtcNow())
            {
                return next;
            }
        }

        return _time.GetUtcNow();
    }

    /// <summary>Runs the action due at <paramref name="now"/>, if any, and returns when the next one is due.</summary>
    private async Task<DateTimeOffset> RunDueAsync(DateTimeOffset now, CancellationToken ct)
    {
        switch (_round)
        {
            case null or { IsFinished: true }:
                if (now < _nextRoundAt)
                {
                    return _nextRoundAt;
                }

                await OpenBettingAsync(now, ct).ConfigureAwait(false);
                return now;

            case { State: RoundState.Betting } betting:
                if (now < betting.BettingEndsAt)
                {
                    return betting.BettingEndsAt;
                }

                await StartFlightAsync(betting, ct).ConfigureAwait(false);
                return now;

            default:
                return await FlyAsync(_round, now, ct).ConfigureAwait(false);
        }
    }

    private async Task<DateTimeOffset> FlyAsync(Round round, DateTimeOffset now, CancellationToken ct)
    {
        if (_crashPoint is not { } crashPoint)
        {
            // Only reachable if a flight was restored without its crash point: read it again (deterministic).
            await LoadCrashPointAsync(round, ct).ConfigureAwait(false);
            return now;
        }

        if (now >= _crashAt)
        {
            await CrashAsync(round, crashPoint, ct).ConfigureAwait(false);
            return now;
        }

        var startedAt = round.StartedAt!.Value;
        var syncAt = startedAt + (SyncInterval * _nextSync);
        if (syncAt >= _crashAt)
        {
            return _crashAt;
        }

        if (now < syncAt)
        {
            return syncAt;
        }

        // Behind schedule: only the latest sync on the grid that is due is sent.
        var latest = Math.Max(_nextSync, (now - startedAt).Ticks / SyncInterval.Ticks);
        syncAt = startedAt + (SyncInterval * latest);
        _nextSync = latest + 1;
        var multiplier = _curve.MultiplierAt(syncAt - startedAt, crashPoint);
        await _broadcaster.Sala(_sala.Id)
            .RoundSync(new RoundSyncMessage(round.Id.Value, syncAt, new ContractMultiplier(multiplier)))
            .ConfigureAwait(false);
        return now;
    }

    private async Task OpenBettingAsync(DateTimeOffset now, CancellationToken ct)
    {
        using var activity = Activities.StartActivity("rounds.open_betting");
        var issued = await NextRoundAsync(ct).ConfigureAwait(false);
        var round = Round.OpenBetting(
            new RoundId(Guid.CreateVersion7(now)),
            _sala.Id,
            _nextNumber,
            issued.ChainId.Value,
            issued.RoundIndex,
            issued.Commitment.ToHex(),
            now + _sala.BettingDuration);
        activity?.SetTag("rounds.sala_id", _sala.Id).SetTag("rounds.round_id", round.Id.Value).SetTag("rounds.round_number", round.Number);

        await _store.SaveAsync(round, ct).ConfigureAwait(false);
        _round = round;
        _nextNumber = round.Number + 1;
        _crashPoint = null;

        RoundsLog.BettingOpened(_logger, _sala.Id, round.Number, round.Id.Value, round.BettingEndsAt);
        await _broadcaster.Sala(_sala.Id)
            .RoundBetting(new RoundBettingMessage(_sala.Id, round.Id.Value, round.Number, round.Commitment, round.BettingEndsAt))
            .ConfigureAwait(false);
    }

    private async Task StartFlightAsync(Round round, CancellationToken ct)
    {
        using var activity = StartRoundActivity("rounds.start_flight", round);

        // The crash point is read before the flight starts and stays in memory only (SPEC-0008 §4, emenda v2).
        var crashPoint = await _fairness
            .GetCrashPointAsync(new ChainId(round.ChainId), round.ChainRoundIndex, _rtp, _cap, ct)
            .ConfigureAwait(false);
        var startedAt = _time.GetUtcNow();
        round.StartFlight(startedAt);
        await _store.SaveAsync(round, ct).ConfigureAwait(false);
        Schedule(round, crashPoint.Value);

        _metrics.RoundStarted(_sala.Id);
        RoundsLog.FlightStarted(_logger, _sala.Id, round.Number, round.Id.Value, startedAt);
        await _broadcaster.Sala(_sala.Id)
            .RoundFlying(new RoundFlyingMessage(round.Id.Value, startedAt, _growthK))
            .ConfigureAwait(false);
    }

    private async Task CrashAsync(Round round, decimal crashPoint, CancellationToken ct)
    {
        using var activity = StartRoundActivity("rounds.crash", round);
        var crashedAt = _crashAt;

        // Only now, after the crash, is the seed revealed (SPEC-0007@2, ADR-0014).
        var revealed = await _fairness
            .RevealAsync(new ChainId(round.ChainId), round.ChainRoundIndex, _rtp, _cap, ct)
            .ConfigureAwait(false);
        if (revealed.CrashPoint.Value != crashPoint)
        {
            RoundsLog.CrashPointMismatch(_logger, _sala.Id, round.Number, round.Id.Value);
        }

        round.Crash(crashPoint, revealed.Seed.ToHex(), crashedAt);
        await _store.SaveAsync(round, ct).ConfigureAwait(false);
        _crashPoint = null;
        _nextRoundAt = crashedAt + _sala.CooldownDuration;

        RoundsLog.Crashed(_logger, _sala.Id, round.Number, round.Id.Value, crashPoint, crashedAt);
        await _broadcaster.Sala(_sala.Id)
            .RoundCrashed(new RoundCrashedMessage(round.Id.Value, new ContractMultiplier(crashPoint), crashedAt, round.RevealedSeed!))
            .ConfigureAwait(false);
        _metrics.RoundCrashed(_sala.Id, _time.GetUtcNow() - crashedAt);
    }

    private async Task AnnulAsync(Round round, string reason, CancellationToken ct)
    {
        using var activity = StartRoundActivity("rounds.annul", round);
        var now = _time.GetUtcNow();
        round.Annul(reason, now);
        await _store.SaveAsync(round, ct).ConfigureAwait(false);
        _crashPoint = null;
        _nextRoundAt = now + _sala.CooldownDuration;

        _metrics.RoundAnnulled(_sala.Id);
        RoundsLog.Annulled(_logger, _sala.Id, round.Number, round.Id.Value, reason);
        await _broadcaster.Sala(_sala.Id).RoundAnnulled(new RoundAnnulledMessage(round.Id.Value, reason)).ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes the latest round of the room (ADR-0018): a finished one sets the next number and the pause; an interrupted
    /// one continues with its persisted <c>startedAt</c> and the same crash point, or is annulled beyond the deadline.
    /// </summary>
    private async Task ResumeAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var latest = await _store.FindLatestAsync(_sala.Id, ct).ConfigureAwait(false);
        if (latest is null)
        {
            // A room's first round also follows a pause, so the clients of a new room see its bets open.
            _nextRoundAt = now + _sala.CooldownDuration;
            return;
        }

        _round = latest;
        _nextNumber = latest.Number + 1;
        switch (latest.State)
        {
            case RoundState.Crashed:
                _nextRoundAt = latest.CrashedAt!.Value + _sala.CooldownDuration;
                return;
            case RoundState.Annulled:
                _nextRoundAt = (latest.AnnulledAt ?? now) + _sala.CooldownDuration;
                return;
        }

        RoundsLog.Resumed(_logger, _sala.Id, latest.Number, latest.Id.Value, latest.State.ToString());

        // The previous leader was alive at least when it last moved the round (bets opened or flight started).
        var lastLeaderActivity = latest.StartedAt ?? (latest.BettingEndsAt - _sala.BettingDuration);
        if (_leaderLastSeenAt is { } seen && seen > lastLeaderActivity)
        {
            lastLeaderActivity = seen;
        }

        if (now - lastLeaderActivity > ResumeDeadline)
        {
            await AnnulAsync(latest, ResumeDeadlineReason, ct).ConfigureAwait(false);
            return;
        }

        if (latest.State == RoundState.Flying)
        {
            await LoadCrashPointAsync(latest, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Reads the crash point of a resumed flight again (deterministic) and schedules its crash and syncs.</summary>
    private async Task LoadCrashPointAsync(Round round, CancellationToken ct)
    {
        var crashPoint = await _fairness
            .GetCrashPointAsync(new ChainId(round.ChainId), round.ChainRoundIndex, _rtp, _cap, ct)
            .ConfigureAwait(false);
        Schedule(round, crashPoint.Value);

        // Syncs continue on the grid of the original flight.
        var elapsed = _time.GetUtcNow() - round.StartedAt!.Value;
        _nextSync = Math.Max(1, (elapsed.Ticks / SyncInterval.Ticks) + 1);
    }

    private void Schedule(Round round, decimal crashPoint)
    {
        _crashPoint = crashPoint;
        _crashAt = round.StartedAt!.Value + _curve.TimeToCrash(crashPoint);
        _nextSync = 1;
    }

    /// <summary>The next round of the room's chain; creates a chain when the room has none left (or one Rounds did not record).</summary>
    private async Task<RoundSeedCommitment> NextRoundAsync(CancellationToken ct)
    {
        RoundSeedCommitment issued;
        try
        {
            issued = await _fairness.NextRoundAsync(_fairnessSala, ct).ConfigureAwait(false);
        }
        catch (ChainExhaustedException)
        {
            await CreateChainAsync(ct).ConfigureAwait(false);
            return await _fairness.NextRoundAsync(_fairnessSala, ct).ConfigureAwait(false);
        }

        if (_chains is not null && await _chains.FindAsync(issued.ChainId.Value, ct).ConfigureAwait(false) is null)
        {
            // Its terminal hash is unknown to Rounds, so its rounds could not be verified: play a new chain instead.
            RoundsLog.UnknownChain(_logger, _sala.Id, issued.ChainId.Value);
            await CreateChainAsync(ct).ConfigureAwait(false);
            return await _fairness.NextRoundAsync(_fairnessSala, ct).ConfigureAwait(false);
        }

        return issued;
    }

    private async Task CreateChainAsync(CancellationToken ct)
    {
        var chain = await _fairness.CreateChainAsync(_fairnessSala, _sala.ChainLength, ct).ConfigureAwait(false);
        if (_chains is not null)
        {
            await _chains.AddAsync(
                new RoundChain(chain.ChainId.Value, _sala.Id, chain.TerminalHash.ToHex(), chain.Salt.ToHex(), chain.Length, _time.GetUtcNow()),
                ct).ConfigureAwait(false);
        }

        RoundsLog.ChainCreated(_logger, _sala.Id, chain.ChainId.Value, chain.Length);
    }

    private Activity? StartRoundActivity(string name, Round round) =>
        Activities.StartActivity(name)?
            .SetTag("rounds.sala_id", _sala.Id)
            .SetTag("rounds.round_id", round.Id.Value)
            .SetTag("rounds.round_number", round.Number);
}
