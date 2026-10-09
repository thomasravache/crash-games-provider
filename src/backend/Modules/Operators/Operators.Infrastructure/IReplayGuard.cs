namespace Apogeu.Operators.Infrastructure;

/// <summary>Remembers the signed requests already seen, to reject replays inside the signature window (SPEC-0009 §4).</summary>
public interface IReplayGuard
{
    /// <summary>
    /// Registers <paramref name="fingerprint"/> for <paramref name="retention"/>; <see langword="false"/> when it was already
    /// registered (a replay). The fingerprint must not be a secret: callers pass a hash.
    /// </summary>
    Task<bool> TryRegisterAsync(string fingerprint, TimeSpan retention, CancellationToken cancellationToken);
}
