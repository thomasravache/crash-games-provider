namespace Apogeu.ServiceDefaults.Messaging;

/// <summary>
/// Whether the Host's messaging (Wolverine, ADR-0002/ADR-0028) has started: a module waits for it before writing through the
/// transactional outbox, so no message is stored while it cannot be delivered.
/// </summary>
public interface IMessagingReadiness
{
    bool IsReady { get; }

    /// <summary>Completes once the messaging started.</summary>
    Task WhenReadyAsync(CancellationToken ct);
}
