namespace Apogeu.Operators.Features.Backoffice;

/// <summary>Realm roles of the <c>apogeu</c> Keycloak realm (SPEC-0009 §2, ADR-0015).</summary>
public static class BackofficeRoles
{
    /// <summary>Provider staff; MFA required (ADR-0015).</summary>
    public const string ProviderAdmin = "provider-admin";

    public const string OperatorAdmin = "operator-admin";

    public const string OperatorViewer = "operator-viewer";
}
