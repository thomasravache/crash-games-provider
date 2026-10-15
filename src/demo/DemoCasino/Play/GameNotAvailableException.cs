namespace Apogeu.DemoCasino.Play;

/// <summary>The Apogeu refused the launch with <c>GAME_NOT_AVAILABLE</c> (SPEC-0045 §6): the casino answers 404 with it.</summary>
internal sealed class GameNotAvailableException(string gameCode)
    : Exception($"The game '{gameCode}' is not available for the casino.");
