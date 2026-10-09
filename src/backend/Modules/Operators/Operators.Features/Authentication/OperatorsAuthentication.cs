namespace Apogeu.Operators.Features.Authentication;

/// <summary>Authentication schemes and authorization policies of the three identities of ADR-0015.</summary>
public static class OperatorsAuthentication
{
    /// <summary>Operator systems: HMAC-signed requests (<c>X-Apogeu-Key</c>, <c>X-Apogeu-Timestamp</c>, <c>X-Apogeu-Signature</c>).</summary>
    public const string OperatorSignatureScheme = "OperatorSignature";

    /// <summary>Players: the session JWT signed by the provider (HTTP bearer, <c>access_token</c> on the hubs).</summary>
    public const string PlayerSessionScheme = Sessions.PlayerSessionClaims.AuthenticationScheme;

    /// <summary>Backoffice users: standard OIDC bearer tokens of the Keycloak realm <c>apogeu</c>.</summary>
    public const string BackofficeScheme = "Backoffice";

    /// <summary>Requests signed with an active key of a registered operator.</summary>
    public const string OperatorPolicy = "Operator";

    /// <summary>A valid player session.</summary>
    public const string PlayerPolicy = BuildingBlocks.Security.SecurityPolicies.PlayerSession;

    /// <summary>A backoffice token of the realm with at least one Apogeu role.</summary>
    public const string BackofficePolicy = "Backoffice";

    /// <summary>Claim with the operator of an authenticated operator request or player session.</summary>
    public const string OperatorClaim = Sessions.PlayerSessionClaims.Operator;

    /// <summary>Claim with the id of the API key of an authenticated operator request.</summary>
    public const string KeyClaim = "key";
}
