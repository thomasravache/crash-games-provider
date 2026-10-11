using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.CashOut;
using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Features.Salas;
using Apogeu.Rounds.Infrastructure.Persistence;
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

    /// <summary>
    /// Reason recorded on a round whose revealed crash point differs from the one read at the start of its flight: such a
    /// round is never published as crashed.
    /// </summary>
    public const string CrashPointMismatchReason = "CRASH_POINT_MISMATCH";

    /// <summary>Upper bound of actions run by one step, so a step always returns even if the clock jumps far ahead.</summary>
    private const int MaxActionsPerStep = 64;

    /// <summary>How many of the latest flights the leader still decides cash-outs for (a pending request is decided late).</summary>
    private const int RememberedFlights = 8;

    /// <summary>When an automatic cash-out whose publication failed is tried again.</summary>
    private static readonly TimeSpan AutoRetryDelay = TimeSpan.FromMilliseconds(100);

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
    private readonly SalaId _fairnessSala;
    private readonly ISalaSnapshotPublisher? _snapshots;
    private readonly IRoundOutbox? _outbox;

    // The last crashed rounds of the room, newest first: kept only when the state is published (IMP-02).
    private RoundSummary[] _history = [];

    private bool _resumed;
    private Round? _round;
    private long _nextNumber = 1;
    private DateTimeOffset _nextRoundAt;

    // The crash point of the round in flight: in memory only, never persisted, logged or sent before the crash.
    private decimal? _crashPoint;
    private DateTimeOffset _crashAt;
    private long _nextSync;

    // The growth curve of the round in flight: the one of the parameters it was opened with.
    private GrowthCurve _curve;

    // SPEC-0023: the flights this leader conducted (the latest ones), read by the cash-out decisions of any thread; their crash
    // instant never leaves this instance, only the decision does (ADR-0014).
    private readonly ConcurrentDictionary<RoundId, CashOutFlight> _flights = new();

    // The automatic cash-outs scheduled for the round in flight, executed at their target instant (SPEC-0023 §6).
    private readonly Lock _autoGate = new();
    private readonly List<ScheduledAutoCashOut> _autos = [];
    private IAutoCashOutSink? _autoSink;
    private TaskCompletionSource _wake = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
        : this(sala, fairness, store, broadcaster, metrics, time, logger, chains, leaderLastSeenAt, snapshots: null)
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
    /// The last instant the previous leader of the room was seen alive, when known.
    /// </param>
    /// <param name="snapshots">
    /// Receives the state of the room (current round and last crashed rounds) after every transition is persisted and
    /// before it is broadcast, so <c>JoinSala</c> on the leader never reads the database (IMP-02).
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
        DateTimeOffset? leaderLastSeenAt,
        ISalaSnapshotPublisher? snapshots)
        : this(sala, fairness, store, broadcaster, metrics, time, logger, chains, leaderLastSeenAt, snapshots, outbox: null)
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
    /// <param name="leaderLastSeenAt">The last instant the previous leader of the room was seen alive, when known.</param>
    /// <param name="snapshots">Receives the state of the room after every transition is persisted.</param>
    /// <param name="outbox">
    /// Saves each transition together with its integration event (<see cref="RoundBettingOpened"/>, <see cref="RoundFlying"/>,
    /// <see cref="RoundCrashed"/>, <see cref="RoundAnnulled"/>) in one transaction (SPEC-0022, ADR-0002); without it the
    /// transitions are saved in <paramref name="store"/> and no event is published.
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
        DateTimeOffset? leaderLastSeenAt,
        ISalaSnapshotPublisher? snapshots,
        IRoundOutbox? outbox)
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
        _snapshots = snapshots;
        _outbox = outbox;
        _curve = new GrowthCurve(sala.GrowthK);
        _fairnessSala = SalaIdentity.ToFairness(sala.Id);
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

        // The automatic cash-outs due run before anything else of the instant, the crash included (t_alvo < t_c).
        await RunDueAutosAsync(now, ct).ConfigureAwait(false);
        if (now >= _crashAt)
        {
            await CrashAsync(round, crashPoint, ct).ConfigureAwait(false);
            return now;
        }

        var next = await SyncAsync(round, crashPoint, now).ConfigureAwait(false);
        return NextAutoAt() is { } auto && auto < next ? auto : next;
    }

    /// <summary>Sends the sync due at <paramref name="now"/>, if any, and returns when the next sync or the crash is due.</summary>
    private async Task<DateTimeOffset> SyncAsync(Round round, decimal crashPoint, DateTimeOffset now)
    {
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
            now + _sala.BettingDuration,
            RoundParameters.Of(_sala));
        activity?.SetTag("rounds.sala_id", _sala.Id).SetTag("rounds.round_id", round.Id.Value).SetTag("rounds.round_number", round.Number);

        await SaveAsync(round, new RoundBettingOpened(_sala.Id, round.Id, round.Number, ContractInstant(round.BettingEndsAt)), ct).ConfigureAwait(false);
        _round = round;
        _nextNumber = round.Number + 1;
        _crashPoint = null;
        PublishSnapshot();

        RoundsLog.BettingOpened(_logger, _sala.Id, round.Number, round.Id.Value, round.BettingEndsAt);
        await _broadcaster.Sala(_sala.Id)
            .RoundBetting(new RoundBettingMessage(_sala.Id, round.Id.Value, round.Number, round.Commitment, round.BettingEndsAt))
            .ConfigureAwait(false);
    }

    private async Task StartFlightAsync(Round round, CancellationToken ct)
    {
        using var activity = StartRoundActivity("rounds.start_flight", round);

        // The crash point is read before the flight starts and stays in memory only (SPEC-0008 §4, emenda v2).
        var parameters = ParametersOf(round);
        var crashPoint = await _fairness
            .GetCrashPointAsync(new ChainId(round.ChainId), round.ChainRoundIndex, new Rtp(parameters.Rtp), new FairnessMultiplier(parameters.Cap), ct)
            .ConfigureAwait(false);
        var startedAt = _time.GetUtcNow();
        round.StartFlight(startedAt);

        // Known to the cash-out decisions before RoundFlying makes any bet active (SPEC-0023).
        Schedule(round, crashPoint.Value);
        await SaveAsync(round, new RoundFlying(_sala.Id, round.Id, ContractInstant(startedAt), (decimal)parameters.GrowthK), ct).ConfigureAwait(false);
        PublishSnapshot();

        _metrics.RoundStarted(_sala.Id);
        RoundsLog.FlightStarted(_logger, _sala.Id, round.Number, round.Id.Value, startedAt);
        await _broadcaster.Sala(_sala.Id)
            .RoundFlying(new RoundFlyingMessage(round.Id.Value, startedAt, FormatGrowthK(parameters.GrowthK)))
            .ConfigureAwait(false);
    }

    private async Task CrashAsync(Round round, decimal crashPoint, CancellationToken ct)
    {
        using var activity = StartRoundActivity("rounds.crash", round);
        var crashedAt = _crashAt;

        // Only now, after the crash, is the seed revealed (SPEC-0007@2, ADR-0014).
        var parameters = ParametersOf(round);
        var revealed = await _fairness
            .RevealAsync(new ChainId(round.ChainId), round.ChainRoundIndex, new Rtp(parameters.Rtp), new FairnessMultiplier(parameters.Cap), ct)
            .ConfigureAwait(false);
        if (revealed.CrashPoint.Value != crashPoint)
        {
            // The seed would not verify the crash the room flew: never publish it as a crash.
            RoundsLog.CrashPointMismatch(_logger, _sala.Id, round.Number, round.Id.Value);
            await AnnulAsync(round, CrashPointMismatchReason, ct).ConfigureAwait(false);
            return;
        }

        round.Crash(crashPoint, revealed.Seed.ToHex(), crashedAt);
        await SaveAsync(round, new RoundCrashed(_sala.Id, round.Id, crashPoint, ContractInstant(crashedAt)), ct).ConfigureAwait(false);
        _crashPoint = null;
        DropAutos();
        EndFlight(round.Id, crashedAt, annulled: false);
        _nextRoundAt = crashedAt + _sala.CooldownDuration;
        if (_snapshots is not null)
        {
            _history = [SalaSnapshotQuery.Summary(round), .. _history.Take(SalaSnapshotQuery.HistorySize - 1)];
        }

        PublishSnapshot();

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
        EndFlight(round.Id, now, annulled: true);
        round.Annul(reason, now);
        await SaveAsync(round, new RoundAnnulled(_sala.Id, round.Id, reason), ct).ConfigureAwait(false);
        _crashPoint = null;
        DropAutos();
        _nextRoundAt = now + _sala.CooldownDuration;
        PublishSnapshot();

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
        if (_snapshots is not null)
        {
            // The only history read of the term: from now on the conductor keeps it from its own crashes.
            var crashed = await _store.ListCrashedAsync(_sala.Id, SalaSnapshotQuery.HistorySize, ct).ConfigureAwait(false);
            _history = [.. crashed.Select(SalaSnapshotQuery.Summary)];
        }

        await ResumeLatestAsync(ct).ConfigureAwait(false);
        PublishSnapshot();
    }

    private async Task ResumeLatestAsync(CancellationToken ct)
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

        RoundsLog.Resumed(_logger, _sala.Id, latest.Number, latest.Id.Value, latest.State);

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
        // The parameters the round was opened with, not the room's current configuration (it may have changed meanwhile).
        var parameters = ParametersOf(round);
        var crashPoint = await _fairness
            .GetCrashPointAsync(new ChainId(round.ChainId), round.ChainRoundIndex, new Rtp(parameters.Rtp), new FairnessMultiplier(parameters.Cap), ct)
            .ConfigureAwait(false);
        Schedule(round, crashPoint.Value);

        // Syncs continue on the grid of the original flight.
        var elapsed = _time.GetUtcNow() - round.StartedAt!.Value;
        _nextSync = Math.Max(1, (elapsed.Ticks / SyncInterval.Ticks) + 1);
    }

    private void Schedule(Round round, decimal crashPoint)
    {
        _curve = new GrowthCurve(ParametersOf(round).GrowthK);
        _crashPoint = crashPoint;
        _crashAt = round.StartedAt!.Value + _curve.TimeToCrash(crashPoint);
        _nextSync = 1;
        RememberFlight(new CashOutFlight(round.Id, round.StartedAt.Value, _crashAt, crashPoint, _curve));
    }

    /// <summary>Completes when an automatic cash-out is scheduled: the leader loop waits on it with the next instant.</summary>
    internal Task Woken => Volatile.Read(ref _wake).Task;

    /// <summary>The sink of the automatic cash-outs this conductor executes (SPEC-0023), set by its <see cref="LeaderCashOutArbiter"/>.</summary>
    internal void Attach(IAutoCashOutSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _autoSink = sink;
    }

    /// <summary>
    /// Decides a manual cash-out of <paramref name="roundId"/> received at <paramref name="receivedAt"/> (SPEC-0023 §6,
    /// latency tolerance 0): accepted with the displayed multiplier when received before the crash instant (not rounded),
    /// too late otherwise. A round this conductor does not have in memory is <see cref="CashOutUndecided"/> (review G4, M2):
    /// see <see cref="DecidePersisted"/> for one that ended.
    /// </summary>
    internal CashOutDecision Decide(RoundId roundId, DateTimeOffset receivedAt) =>
        _flights.TryGetValue(roundId, out var flight)
            ? flight.Annulled ? new CashOutTooLate() : Decide(flight.StartedAt, flight.CrashAt, flight.CrashPoint, flight.Curve, receivedAt)
            : new CashOutUndecided();

    /// <summary>
    /// Decides a cash-out of a round out of this conductor's memory from the round as persisted: a crashed round with the
    /// same rule from its public data (<c>startedAt</c> and crash point), an annulled one too late, any other one
    /// (not ended, or not found) <see cref="CashOutUndecided"/>.
    /// </summary>
    internal CashOutDecision DecidePersisted(Round? round, DateTimeOffset receivedAt)
    {
        switch (round)
        {
            case { State: RoundState.Crashed, StartedAt: { } startedAt, CrashPoint: { } crashPoint }:
                var curve = new GrowthCurve(ParametersOf(round).GrowthK);
                return Decide(startedAt, startedAt + curve.TimeToCrash(crashPoint), crashPoint, curve, receivedAt);
            case { State: RoundState.Annulled }:
                return new CashOutTooLate();
            default:
                return new CashOutUndecided();
        }
    }

    /// <summary>The persisted round <paramref name="roundId"/>, through the conductor's own store.</summary>
    internal Task<Round?> FindRoundAsync(RoundId roundId, CancellationToken ct) => _store.FindAsync(roundId, ct);

    private static CashOutDecision Decide(DateTimeOffset startedAt, DateTimeOffset crashAt, decimal crashPoint, GrowthCurve curve, DateTimeOffset receivedAt) =>
        receivedAt >= crashAt
            ? new CashOutTooLate()
            : new CashOutAccepted(new ContractMultiplier(curve.MultiplierAt(receivedAt - startedAt, crashPoint)));

    /// <summary>
    /// Schedules the automatic cash-out of <paramref name="betId"/> at <paramref name="target"/>: executed at
    /// <c>startedAt + ln(target)/k</c> only if that is before the crash instant (otherwise never) and wakes the leader loop.
    /// </summary>
    internal void ScheduleAuto(RoundId roundId, Guid betId, ContractMultiplier target)
    {
        if (!_flights.TryGetValue(roundId, out var flight) || flight.Ended || target.Value < 1.00m)
        {
            // Unknown here, or the flight already ended: Betting reconciles it with the public crash point (review G4, M1).
            return;
        }

        var at = flight.StartedAt + TimeSpan.FromMilliseconds(Math.Log((double)target.Value) / flight.Curve.K);
        if (at >= flight.CrashAt)
        {
            // Above the crash: it never executes (the bet loses), and nothing about the crash is told.
            return;
        }

        lock (_autoGate)
        {
            if (_autos.Exists(auto => auto.BetId == betId))
            {
                return;
            }

            _autos.Add(new ScheduledAutoCashOut(roundId, betId, target, at));
        }

        Interlocked.Exchange(ref _wake, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    }

    /// <summary>Executes, in the order of their instants, the automatic cash-outs due at <paramref name="now"/> before the crash.</summary>
    private async Task RunDueAutosAsync(DateTimeOffset now, CancellationToken ct)
    {
        List<ScheduledAutoCashOut> due;
        lock (_autoGate)
        {
            due = [.. _autos.Where(auto => auto.DueAt <= now && auto.At < _crashAt).OrderBy(auto => auto.At)];
            _autos.RemoveAll(due.Contains);
        }

        if (due.Count == 0 || _autoSink is not { } sink)
        {
            return;
        }

        foreach (var auto in due)
        {
            try
            {
                await sink.PublishAsync(new AutoCashOutExecuted(_sala.Id, auto.RoundId, auto.BetId, auto.Target, auto.At), ct).ConfigureAwait(false);
                RoundsLog.AutoCashOutExecuted(_logger, _sala.Id, auto.RoundId.Value, auto.BetId);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // One automatic cash-out that cannot be published does not stop the room's flight.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                // Kept and retried shortly (review G4, M1); past the crash, Betting reconciles it with the public crash point.
                RoundsLog.AutoCashOutFailed(_logger, _sala.Id, auto.RoundId.Value, auto.BetId, exception);
                lock (_autoGate)
                {
                    _autos.Add(auto with { RetryAt = _time.GetUtcNow() + AutoRetryDelay });
                }
            }
        }
    }

    /// <summary>The instant of the next automatic cash-out scheduled before the crash, if any.</summary>
    private DateTimeOffset? NextAutoAt()
    {
        lock (_autoGate)
        {
            return _autos.Count == 0 ? null : _autos.Min(auto => auto.DueAt);
        }
    }

    /// <summary>The flight ended: its automatic cash-outs not executed never will be.</summary>
    private void DropAutos()
    {
        lock (_autoGate)
        {
            _autos.Clear();
        }
    }

    private void RememberFlight(CashOutFlight flight)
    {
        _flights[flight.RoundId] = flight;
        foreach (var old in _flights.Values.OrderByDescending(f => f.StartedAt).Skip(RememberedFlights))
        {
            _flights.TryRemove(old.RoundId, out _);
        }
    }

    /// <summary>The flight ended (crashed, or annulled at <paramref name="at"/>, which also ends it for the decisions).</summary>
    private void EndFlight(RoundId roundId, DateTimeOffset at, bool annulled)
    {
        if (_flights.TryGetValue(roundId, out var flight))
        {
            _flights[roundId] = flight with { CrashAt = at < flight.CrashAt ? at : flight.CrashAt, Ended = true, Annulled = annulled };
        }
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

    /// <summary>An instant as the contract carries it (ADR-0008: milliseconds), so an event matches the hub message of the round.</summary>
    private static DateTimeOffset ContractInstant(DateTimeOffset instant) =>
        new(instant.Ticks - (instant.Ticks % TimeSpan.TicksPerMillisecond), instant.Offset);

    /// <summary>Saves a transition of the round, with its integration event when the conductor has an outbox.</summary>
    private Task SaveAsync(Round round, object integrationEvent, CancellationToken ct) =>
        _outbox is null ? _store.SaveAsync(round, ct) : _outbox.SaveAsync(round, integrationEvent, ct);

    /// <summary>Publishes the state of the room as persisted: the crash point of a round in flight is never part of it.</summary>
    private void PublishSnapshot() =>
        _snapshots?.Publish(_round is null ? null : SalaSnapshotQuery.Current(_round), _history);

    /// <summary>The parameters the round is played with; the room's current configuration for a round opened without them.</summary>
    private RoundParameters ParametersOf(Round round) => round.Parameters ?? RoundParameters.Of(_sala);

    private Activity? StartRoundActivity(string name, Round round) =>
        Activities.StartActivity(name)?
            .SetTag("rounds.sala_id", _sala.Id)
            .SetTag("rounds.round_id", round.Id.Value)
            .SetTag("rounds.round_number", round.Number);
}
