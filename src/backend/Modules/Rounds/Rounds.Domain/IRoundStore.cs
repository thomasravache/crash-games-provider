using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Domain;

/// <summary>Persistence of the rounds (schema <c>rounds</c>, ADR-0010); the source of truth for resuming a room.</summary>
public interface IRoundStore
{
    Task<Round?> FindAsync(RoundId id, CancellationToken ct);

    /// <summary>The round with the highest number in the room, whatever its state.</summary>
    Task<Round?> FindLatestAsync(string salaId, CancellationToken ct);

    /// <summary>The last <paramref name="limit"/> crashed rounds of the room, newest first.</summary>
    Task<IReadOnlyList<Round>> ListCrashedAsync(string salaId, int limit, CancellationToken ct);

    /// <summary>Inserts or updates the round.</summary>
    Task SaveAsync(Round round, CancellationToken ct);
}
