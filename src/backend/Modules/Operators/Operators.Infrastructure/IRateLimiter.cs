namespace Apogeu.Operators.Infrastructure;

/// <summary>
/// Sliding-window rate limit shared by every instance of the API (Redis). The window follows the instant the caller passes
/// (the injected clock, ADR-0009), not the clock of the store.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Takes one permit of <paramref name="partition"/> at <paramref name="now"/>: <see langword="true"/> when fewer than
    /// <paramref name="permitLimit"/> permits were taken in the <paramref name="window"/> that ends at <paramref name="now"/>.
    /// </summary>
    Task<bool> TryAcquireAsync(string partition, int permitLimit, TimeSpan window, DateTimeOffset now, CancellationToken cancellationToken);
}
