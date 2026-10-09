using System.Security.Claims;
using System.Text.Json;

namespace Apogeu.Operators.Features.Backoffice;

/// <summary>
/// Reads the Apogeu identity out of a Keycloak access token of the realm <c>apogeu</c>: the realm roles
/// (<c>realm_access.roles</c>), kept only when they are one of <see cref="BackofficeRoles"/>, and the organization
/// (= operator) of the user (<c>organization</c>, from the Keycloak organization scope).
/// </summary>
internal static class BackofficeClaims
{
    public const string RealmAccess = "realm_access";

    public const string Organization = "organization";

    private static readonly string[] ApogeuRoles = [BackofficeRoles.ProviderAdmin, BackofficeRoles.OperatorAdmin, BackofficeRoles.OperatorViewer];

    /// <summary>The Apogeu roles of <paramref name="user"/>, in the order of <see cref="BackofficeRoles"/>; empty when none.</summary>
    public static IReadOnlyList<string> RolesOf(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in user.FindAll(RealmAccess))
        {
            foreach (var role in RealmRoles(claim.Value))
            {
                granted.Add(role);
            }
        }

        return [.. ApogeuRoles.Where(granted.Contains)];
    }

    /// <summary>The operator of the user (first organization alias), or <see langword="null"/> for provider staff.</summary>
    public static string? OperatorOf(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        foreach (var claim in user.FindAll(Organization))
        {
            if (FirstOrganization(claim.Value) is { } alias)
            {
                return alias;
            }
        }

        return null;
    }

    private static IEnumerable<string> RealmRoles(string json)
    {
        if (!TryParse(json, out var document))
        {
            return [];
        }

        using (document)
        {
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("roles", out var roles)
                && roles.ValueKind == JsonValueKind.Array
                ? [.. roles.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.String).Select(r => r.GetString()!)]
                : [];
        }
    }

    /// <summary>The organization claim is an alias, an array of aliases or an object keyed by alias (Keycloak mapper options).</summary>
    private static string? FirstOrganization(string value)
    {
        if (!TryParse(value, out var document))
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        using (document)
        {
            var root = document.RootElement;
            return root.ValueKind switch
            {
                JsonValueKind.Array => root.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()).FirstOrDefault(),
                JsonValueKind.Object => root.EnumerateObject().Select(p => p.Name).FirstOrDefault(),
                JsonValueKind.String => root.GetString(),
                _ => null,
            };
        }
    }

    private static bool TryParse(string value, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            document = null!;
            return false;
        }
    }
}
