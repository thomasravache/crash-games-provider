namespace Apogeu.Betting.Tests.Support;

/// <summary>What the <see cref="ObservedCashOutArbiter"/> of one instance saw, and the delay the test armed on it.</summary>
internal sealed class ArbiterProbe
{
    private readonly Lock _gate = new();
    private readonly List<ArbiterCall> _calls = [];
    private readonly List<ArbiterCall> _requests = [];
    private TimeSpan? _leaderDelay;
    private TaskCompletionSource? _hold;

    public IReadOnlyList<ArbiterCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>
    /// Every decision asked of this instance, recorded when asked (before it is answered, forwarded or failed), with a
    /// <see langword="null"/> decision.
    /// </summary>
    public IReadOnlyList<ArbiterCall> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Holds every decision this instance runs while leading until <see cref="ReleaseLeader"/> (at most 60 s).</summary>
    public void HoldLeaderDecisions()
    {
        lock (_gate)
        {
            _hold ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Lets the decisions held by <see cref="HoldLeaderDecisions"/> run.</summary>
    public void ReleaseLeader()
    {
        TaskCompletionSource? hold;
        lock (_gate)
        {
            hold = _hold;
            _hold = null;
        }

        hold?.TrySetResult();
    }

    /// <summary>The hold armed by <see cref="HoldLeaderDecisions"/>, if any.</summary>
    public Task? LeaderHold()
    {
        lock (_gate)
        {
            return _hold?.Task;
        }
    }

    /// <summary>Holds the next decision this instance runs while leading for <paramref name="delay"/> (the leader does not answer).</summary>
    public void DelayNextLeaderDecision(TimeSpan delay)
    {
        lock (_gate)
        {
            _leaderDelay = delay;
        }
    }

    public TimeSpan? TakeLeaderDelay()
    {
        lock (_gate)
        {
            var delay = _leaderDelay;
            _leaderDelay = null;
            return delay;
        }
    }

    public void Asked(ArbiterCall request)
    {
        lock (_gate)
        {
            _requests.Add(request);
        }
    }

    public void Record(ArbiterCall call)
    {
        lock (_gate)
        {
            _calls.Add(call);
        }
    }

    public string Describe() => string.Join(", ", Calls);
}
