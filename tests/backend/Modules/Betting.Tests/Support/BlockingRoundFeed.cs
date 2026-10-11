using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Decorator of the module's <see cref="IRoundFeed"/> (IT-07): once armed, the next <see cref="RoundBettingOpened"/> is
/// applied to the real feed and then held until the test releases it, so a bet sent meanwhile races the activation of the
/// room's queued bets (the round is open, its queued bets are not activated yet).
/// </summary>
internal sealed class BlockingRoundFeed(IRoundFeed inner) : IRoundFeed
{
    private static readonly TimeSpan MaxHold = TimeSpan.FromSeconds(30);

    private readonly TaskCompletionSource<RoundBettingOpened> _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _armed;

    /// <summary>Completes when the armed opening is being held.</summary>
    public Task<RoundBettingOpened> Held => _held.Task;

    public void HoldNextOpening() => Volatile.Write(ref _armed, 1);

    public void Release() => _release.TrySetResult();

    public void Apply(RoundBettingOpened opened)
    {
        inner.Apply(opened);
        if (Interlocked.Exchange(ref _armed, 0) == 1)
        {
            _held.TrySetResult(opened);

            // Apply is synchronous: hold the handler's thread (bounded, so a forgotten release cannot hang the run).
            _release.Task.Wait(MaxHold);
        }
    }

    public void Apply(RoundFlying flying) => inner.Apply(flying);

    public void Apply(RoundCrashed crashed) => inner.Apply(crashed);

    public void Apply(RoundAnnulled annulled) => inner.Apply(annulled);

    public RoundWindow? Current(string salaId) => inner.Current(salaId);

    /// <summary>Forwarded, so the tests run on the production phase source (the default body would read <see cref="Current"/>).</summary>
    public Task<RoundWindow?> CurrentAsync(string salaId, CancellationToken ct) => inner.CurrentAsync(salaId, ct);
}
