namespace Apogeu.Betting.Tests.Support;

/// <summary>What the <see cref="ObservedCashOutArbiter"/> of one instance saw, and the delay the test armed on it.</summary>
internal sealed class ArbiterProbe
{
    private readonly Lock _gate = new();
    private readonly List<ArbiterCall> _calls = [];
    private TimeSpan? _leaderDelay;

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

    public void Record(ArbiterCall call)
    {
        lock (_gate)
        {
            _calls.Add(call);
        }
    }

    public string Describe() => string.Join(", ", Calls);
}
