namespace Apogeu.Rounds.Features.Live;

/// <summary>
/// Reads the state of one room from the store for the joins of an instance that does not lead it (IMP-02): at most one read
/// runs at a time, and every join waiting when it starts shares it. A join is only answered by a read that started after
/// it asked, i.e. after it joined the room group, so no message the group received in between is missed; a burst of joins
/// therefore costs one or two reads and a single database connection per room, instead of one read each.
/// </summary>
internal sealed class CoalescedSalaRead(Func<CancellationToken, Task<SalaLiveViews.LiveState>> read)
{
    private readonly Lock _gate = new();
    private TaskCompletionSource<SalaLiveViews.LiveState>? _waiting;
    private bool _reading;

    public Task<SalaLiveViews.LiveState> ReadAsync(CancellationToken ct)
    {
        TaskCompletionSource<SalaLiveViews.LiveState> batch;
        bool start;
        lock (_gate)
        {
            _waiting ??= new TaskCompletionSource<SalaLiveViews.LiveState>(TaskCreationOptions.RunContinuationsAsynchronously);
            batch = _waiting;
            start = !_reading;
            _reading = true;
        }

        if (start)
        {
            _ = Task.Run(PumpAsync, CancellationToken.None);
        }

        return batch.Task.WaitAsync(ct);
    }

    /// <summary>Runs one read per batch of waiting joins until none is left.</summary>
    private async Task PumpAsync()
    {
        while (true)
        {
            TaskCompletionSource<SalaLiveViews.LiveState> batch;
            lock (_gate)
            {
                if (_waiting is null)
                {
                    _reading = false;
                    return;
                }

                batch = _waiting;
                _waiting = null;
            }

            try
            {
                // Shared by every join of the batch: no single join's cancellation stops it.
                batch.SetResult(await read(CancellationToken.None).ConfigureAwait(false));
            }
#pragma warning disable CA1031 // The failure belongs to the joins of the batch: each of them observes it.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                batch.SetException(exception);
            }
        }
    }
}
