namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Shared switch of the <see cref="GatedRoundStore"/> of one instance (SPEC-0023 IT-09): once armed, every read of the latest
/// round of a room waits until the gate opens, so a new leader takes over the room (its arbiter registered) well before its
/// conductor knows the flight it resumes.
/// </summary>
internal sealed class ResumeGate
{
    private readonly Lock _gate = new();
    private DateTimeOffset _closedUntil = DateTimeOffset.MinValue;

    /// <summary>Delays the reads of the latest round made from now on until <paramref name="duration"/> has passed.</summary>
    public void CloseFor(TimeSpan duration)
    {
        lock (_gate)
        {
            _closedUntil = DateTimeOffset.UtcNow + duration;
        }
    }

    public async Task PassAsync()
    {
        DateTimeOffset until;
        lock (_gate)
        {
            until = _closedUntil;
        }

        var wait = until - DateTimeOffset.UtcNow;
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
