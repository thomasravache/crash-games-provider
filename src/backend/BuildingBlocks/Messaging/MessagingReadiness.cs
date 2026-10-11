namespace Apogeu.BuildingBlocks.Messaging;

/// <summary><see cref="IMessagingReadiness"/> over the Host's messaging lifecycle (its readiness signal and state).</summary>
/// <param name="isReady">Whether the messaging started.</param>
/// <param name="whenReady">Completes once the messaging started.</param>
public sealed class MessagingReadiness(Func<bool> isReady, Func<CancellationToken, Task> whenReady) : IMessagingReadiness
{
    public bool IsReady => isReady();

    public Task WhenReadyAsync(CancellationToken ct) => whenReady(ct);
}
