using Apogeu.Rounds.Domain;

namespace Apogeu.Rounds.Infrastructure.Persistence;

/// <summary>
/// Saves a round together with the integration event of its transition (<c>RoundBettingOpened</c>, <c>RoundFlying</c>,
/// <c>RoundCrashed</c> or <c>RoundAnnulled</c>, SPEC-0022): both are committed in the same transaction of the schema
/// <c>rounds</c> (Wolverine's transactional outbox, ADR-0002), so an event is delivered if and only if its round was saved.
/// </summary>
public interface IRoundOutbox
{
    Task SaveAsync(Round round, object integrationEvent, CancellationToken ct);
}
