namespace Apogeu.DemoCasino.Lobby;

/// <summary>
/// The Apogeu could not answer the catalog (SPEC-0044 §5) or the launch (SPEC-0045 §6): the casino answers 502
/// PROVIDER_UNAVAILABLE.
/// </summary>
internal sealed class ProviderUnavailableException(string message, Exception? innerException)
    : Exception(message, innerException);
