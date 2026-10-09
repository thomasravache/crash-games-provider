namespace Apogeu.Operators.Features.Sessions;

/// <summary>Claims of the player session JWT signed by the provider (SPEC-0009 §4).</summary>
public static class PlayerSessionClaims
{
    /// <summary>Pseudonymous player id.</summary>
    public const string Subject = "sub";

    public const string Operator = "op";

    public const string Sala = "sala";

    public const string Currency = "cur";

    public const string Mode = "mode";

    /// <summary>Lifetime of a session (ADR-0015: ≤ 8 h).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    /// <summary>Authentication scheme of the player session (HTTP bearer and <c>access_token</c> on the hubs).</summary>
    public const string AuthenticationScheme = "PlayerSession";
}
