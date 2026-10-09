namespace Apogeu.Operators.Features.Configuration;

/// <summary>Configuration section <c>Security:Backoffice</c>: standard OIDC JwtBearer against Keycloak (ADR-0015, no vendor SDK).</summary>
public sealed class BackofficeAuthenticationOptions
{
    public const string SectionName = "Security:Backoffice";

    /// <summary>Issuer of the <c>apogeu</c> realm, e.g. <c>{keycloak}/realms/apogeu</c>.</summary>
    public Uri? Authority { get; set; }

    /// <summary>Audience the backoffice API requires in the access token.</summary>
    public string? Audience { get; set; }

    /// <summary>Only false for a local Keycloak over plain HTTP.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}
