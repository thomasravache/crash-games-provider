using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Operators.Contracts;

/// <summary>
/// Default configuration of a new sala of a game (SPEC-0020 §6): the Rounds module keeps the configuration the engine uses;
/// the catalog only provides the default for new salas.
/// </summary>
/// <param name="Rtp">Return to player, an exact decimal (for example 0.97).</param>
/// <param name="Cap">Highest crash multiplier (for example 10000.00x).</param>
/// <param name="GrowthK">Growth constant per millisecond of <c>m(t) = e^(k·t)</c>.</param>
/// <param name="BettingSeconds">Length of the betting phase, in seconds.</param>
/// <param name="PauseSeconds">Pause after the crash, in seconds.</param>
public sealed record SalaDefaults(decimal Rtp, Multiplier Cap, double GrowthK, int BettingSeconds, int PauseSeconds);
