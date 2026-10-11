namespace Apogeu.Rounds.Contracts;

/// <summary>
/// The current round of each room, the same on every instance (SPEC-0022 emenda 3): the SPEC-0008 snapshot of the room — in
/// memory on the instance that conducts it, through the shared (coalesced) store read elsewhere.
/// </summary>
public interface ISalaPhase
{
    /// <summary>The current round of the room <paramref name="salaId"/>, or <see langword="null"/> for an unknown room or before its first round.</summary>
    Task<SalaRound?> CurrentAsync(string salaId, CancellationToken ct);
}
