namespace Apogeu.Rounds.Infrastructure.Persistence;

/// <summary>
/// Whether Wolverine's runtime has started (ADR-0002): the <see cref="IRoundOutbox"/> waits for it before saving a transition,
/// so no round event is written while the messaging cannot deliver it.
/// </summary>
public sealed class MessagingReadiness
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsReady => _ready.Task.IsCompletedSuccessfully;

    public void MarkReady() => _ready.TrySetResult();

    public Task WhenReadyAsync(CancellationToken ct) => _ready.Task.WaitAsync(ct);
}
