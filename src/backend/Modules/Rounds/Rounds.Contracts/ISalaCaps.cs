using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Rounds.Contracts;

/// <summary>
/// The cap of each room (SPEC-0022 emenda 2): the largest crash multiplier the room's engine plays, the same value the
/// conductor records on every round it opens. Betting reads it to refuse an automatic cash-out above it.
/// </summary>
public interface ISalaCaps
{
    /// <summary>The cap of the room <paramref name="salaId"/> (its text id), or <see langword="null"/> for an unknown room.</summary>
    Multiplier? CapOf(string salaId);
}
