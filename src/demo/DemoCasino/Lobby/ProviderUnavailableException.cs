namespace Apogeu.DemoCasino.Lobby;

/// <summary>The Apogeu could not answer the catalog (SPEC-0044 §5): the lobby answers 502 PROVIDER_UNAVAILABLE.</summary>
internal sealed class ProviderUnavailableException(string message, Exception? innerException)
    : Exception(message, innerException);
