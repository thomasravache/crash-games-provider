using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Apogeu.Repository.Tests.Support;
using Testcontainers.Keycloak;
using Xunit;

namespace Apogeu.Security.Tests.Support;

/// <summary>
/// Real Keycloak (Testcontainers) started with the realm file the AppHost imports (<c>src/backend/AppHost/Realms</c>), so
/// IT-06 proves the realm that actually ships. Test identities are created by this fixture through the admin REST API
/// with generated secrets; the admin password is generated too (ADR-0014: no real credential).
/// </summary>
public sealed class KeycloakRealm : IAsyncLifetime
{
    public const string RealmName = "apogeu";

    /// <summary>Same image as the TestKit fixture (<c>ContainerImages.Keycloak</c>, internal to the TestKit).</summary>
    private const string Image = "quay.io/keycloak/keycloak:26.6";

    private static readonly string RealmFile = RepositoryRoot.Combine("src", "backend", "AppHost", "Realms", "apogeu-realm.json");

    private readonly string _adminPassword = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    private readonly HttpClient _http = new();
    private KeycloakContainer? _container;

    public Uri BaseAddress => new(_container?.GetBaseAddress() ?? throw new InvalidOperationException("Keycloak is not started."));

    /// <summary>Issuer of the <c>apogeu</c> realm, as Keycloak writes it in the tokens.</summary>
    public Uri Authority => new(BaseAddress, $"realms/{RealmName}");

    public async ValueTask InitializeAsync()
    {
        _container = new KeycloakBuilder(Image)
            .WithUsername("admin")
            .WithPassword(_adminPassword)
            .WithRealm(RealmFile)
            .Build();
        await _container.StartAsync().ConfigureAwait(false);

        // Keycloak requires HTTPS for requests that do not come from a private address; through Docker Desktop's port
        // forwarding the test process is not seen as one, so the test container (plain HTTP only) drops the requirement
        // for the master realm and the imported realm, with the admin credentials generated above.
        const string Kcadm = "/opt/keycloak/bin/kcadm.sh";
        await ExecAsync([Kcadm, "config", "credentials", "--server", "http://localhost:8080", "--realm", "master", "--user", "admin", "--password", _adminPassword]).ConfigureAwait(false);
        await ExecAsync([Kcadm, "update", "realms/master", "-s", "sslRequired=NONE"]).ConfigureAwait(false);
        await ExecAsync([Kcadm, "update", $"realms/{RealmName}", "-s", "sslRequired=NONE"]).ConfigureAwait(false);
    }

    private async Task ExecAsync(IList<string> command)
    {
        var result = await _container!.ExecAsync(command).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            // The command line holds the generated admin password: report only the subcommand.
            throw new InvalidOperationException($"kcadm.sh {command[1]} failed in the Keycloak container (exit {result.ExitCode}): {result.Stderr}");
        }
    }

    /// <summary>An access token of the <c>master</c> realm admin: a real Keycloak token, but from the wrong issuer.</summary>
    public Task<string> MasterAdminTokenAsync(CancellationToken ct) =>
        TokenAsync("master", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "admin",
            ["password"] = _adminPassword,
        }, ct);

    /// <summary>Names of the realm roles of the imported <c>apogeu</c> realm.</summary>
    public async Task<IReadOnlySet<string>> RealmRolesAsync(CancellationToken ct)
    {
        using var request = await AdminRequestAsync(HttpMethod.Get, "roles", ct).ConfigureAwait(false);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return json.RootElement.EnumerateArray().Select(role => role.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates a confidential client with a service account in the <c>apogeu</c> realm, whose tokens carry
    /// <paramref name="audience"/>, and grants its service-account user the given realm roles. Returns its token.
    /// </summary>
    public async Task<string> ServiceAccountTokenAsync(string audience, IReadOnlyList<string> realmRoles, CancellationToken ct)
    {
        var clientId = "it-" + Guid.NewGuid().ToString("N");
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        var client = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["clientId"] = clientId,
            ["enabled"] = true,
            ["publicClient"] = false,
            ["secret"] = secret,
            ["serviceAccountsEnabled"] = true,
            ["standardFlowEnabled"] = false,
            ["directAccessGrantsEnabled"] = false,
            ["protocolMappers"] = new[]
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["name"] = "apogeu-audience",
                    ["protocol"] = "openid-connect",
                    ["protocolMapper"] = "oidc-audience-mapper",
                    ["config"] = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["included.custom.audience"] = audience,
                        ["access.token.claim"] = "true",
                        ["id.token.claim"] = "false",
                    },
                },
            },
        };
        using (var create = await AdminRequestAsync(HttpMethod.Post, "clients", ct).ConfigureAwait(false))
        {
            create.Content = JsonContent.Create(client);
            using var created = await _http.SendAsync(create, ct).ConfigureAwait(false);
            created.EnsureSuccessStatusCode();
        }

        var internalId = await SingleIdAsync($"clients?clientId={clientId}", ct).ConfigureAwait(false);
        var userId = await IdAsync($"clients/{internalId}/service-account-user", ct).ConfigureAwait(false);
        var roles = new List<JsonElement>();
        foreach (var role in realmRoles)
        {
            using var get = await AdminRequestAsync(HttpMethod.Get, $"roles/{Uri.EscapeDataString(role)}", ct).ConfigureAwait(false);
            using var found = await _http.SendAsync(get, ct).ConfigureAwait(false);
            Assert.True(found.IsSuccessStatusCode, $"The imported realm '{RealmName}' has no realm role '{role}' (SPEC-0009 §2).");
            roles.Add(JsonDocument.Parse(await found.Content.ReadAsStringAsync(ct).ConfigureAwait(false)).RootElement.Clone());
        }

        if (roles.Count > 0)
        {
            using var assign = await AdminRequestAsync(HttpMethod.Post, $"users/{userId}/role-mappings/realm", ct).ConfigureAwait(false);
            assign.Content = JsonContent.Create(roles);
            using var assigned = await _http.SendAsync(assign, ct).ConfigureAwait(false);
            assigned.EnsureSuccessStatusCode();
        }

        return await TokenAsync(RealmName, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        }, ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<string> TokenAsync(string realm, Dictionary<string, string> form, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await _http.PostAsync(new Uri(BaseAddress, $"realms/{realm}/protocol/openid-connect/token"), content, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Assert.True(response.IsSuccessStatusCode, $"Keycloak refused the token request on realm '{realm}': {(int)response.StatusCode} {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<HttpRequestMessage> AdminRequestAsync(HttpMethod method, string relative, CancellationToken ct)
    {
        var token = await MasterAdminTokenAsync(ct).ConfigureAwait(false);
        var request = new HttpRequestMessage(method, new Uri(BaseAddress, $"admin/realms/{RealmName}/{relative}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> SingleIdAsync(string relative, CancellationToken ct)
    {
        using var request = await AdminRequestAsync(HttpMethod.Get, relative, ct).ConfigureAwait(false);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return json.RootElement.EnumerateArray().Single().GetProperty("id").GetString()!;
    }

    private async Task<string> IdAsync(string relative, CancellationToken ct)
    {
        using var request = await AdminRequestAsync(HttpMethod.Get, relative, ct).ConfigureAwait(false);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return json.RootElement.GetProperty("id").GetString()!;
    }
}
