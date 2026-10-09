namespace Apogeu.Operators.Infrastructure.Persistence;

/// <summary>
/// A row of <c>operators.launch_grants</c>: a launch token issued to an operator for a player (SPEC-0009 §4). Only the
/// hash of the token is stored; the player data is the minimum of ADR-0014 (pseudonymous id, optional nickname, currency,
/// language).
/// </summary>
public sealed class LaunchGrantRecord
{
    /// <summary>Hash of the launch token (lowercase hex of SHA-256); the token itself is never stored.</summary>
    public required string TokenHash { get; set; }

    public required string OperatorId { get; set; }

    public required string SalaId { get; set; }

    public required string PlayerId { get; set; }

    public string? Nickname { get; set; }

    public required string Currency { get; set; }

    public required string Mode { get; set; }

    public required string Language { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>When the token was exchanged for a session; <see langword="null"/> while unused.</summary>
    public DateTimeOffset? RedeemedAt { get; set; }
}
