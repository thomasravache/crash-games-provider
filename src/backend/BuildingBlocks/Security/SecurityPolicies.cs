namespace Apogeu.BuildingBlocks.Security;

/// <summary>
/// Names of the authorization policies of ADR-0015 that endpoints of other modules declare (SPEC-0009: denied by default,
/// each endpoint declares its policy). The Operators module registers them with their schemes.
/// </summary>
public static class SecurityPolicies
{
    /// <summary>A valid player session (the provider's JWT: HTTP bearer, <c>access_token</c> on the hubs).</summary>
    public const string PlayerSession = "Player";
}
