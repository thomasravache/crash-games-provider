using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Decorator of the Betting module's own <see cref="IRoundFeed"/> (IT-05): records, in the order Betting receives them, the
/// Rounds integration events its handlers apply, then hands each one to the real feed.
/// </summary>
internal sealed class RecordingRoundFeed(IRoundFeed inner) : IRoundFeed
{
    private readonly Lock _gate = new();
    private readonly List<object> _received = [];

    public IReadOnlyList<object> Received
    {
        get
        {
            lock (_gate)
            {
                return [.. _received];
            }
        }
    }

    public string Describe() => string.Join(Environment.NewLine, Received);

    public void Apply(RoundBettingOpened opened)
    {
        Record(opened);
        inner.Apply(opened);
    }

    public void Apply(RoundFlying flying)
    {
        Record(flying);
        inner.Apply(flying);
    }

    public void Apply(RoundCrashed crashed)
    {
        Record(crashed);
        inner.Apply(crashed);
    }

    public void Apply(RoundAnnulled annulled)
    {
        Record(annulled);
        inner.Apply(annulled);
    }

    public RoundWindow? Current(string salaId) => inner.Current(salaId);

    private void Record(object received)
    {
        lock (_gate)
        {
            _received.Add(received);
        }
    }
}
