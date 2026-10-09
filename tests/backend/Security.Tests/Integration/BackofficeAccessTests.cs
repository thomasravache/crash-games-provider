using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Integration;

/// <summary>
/// IT-06: the backoffice is protected by standard OIDC bearer tokens of the <c>apogeu</c> realm (ADR-0015), against a real
/// Keycloak with the realm the AppHost imports.
/// </summary>
[Collection(KeycloakDependencies.Name)]
public sealed class BackofficeAccessTests(KeycloakRealm keycloak, PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const string MePath = "/api/v1/backoffice/me";
    private const string Audience = "apogeu-api";

    [Fact(DisplayName = "Dado o realm importado, então ele tem os papéis provider-admin, operator-admin e operator-viewer")]
    [Trait("Spec", "SPEC-0009:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenImportedRealmThenItHasTheBackofficeRoles()
    {
        var roles = await keycloak.RealmRolesAsync(TestContext.Current.CancellationToken);

        foreach (var role in (string[])["provider-admin", "operator-admin", "operator-viewer"])
        {
            Assert.True(roles.Contains(role), $"The realm '{KeycloakRealm.RealmName}' must define the role '{role}'. Roles: {string.Join(", ", roles)}");
        }
    }

    [Fact(DisplayName = "Dado o Keycloak real, quando GET /backoffice/me sem token, então 401")]
    [Trait("Spec", "SPEC-0009:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenNoTokenWhenGettingMeThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri(MePath, UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "Dado o Keycloak real, quando GET /backoffice/me com token de outro emissor (realm master) ou com assinatura quebrada, então 401")]
    [Trait("Spec", "SPEC-0009:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenTokenFromWrongIssuerOrForgedWhenGettingMeThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var admin = await keycloak.ServiceAccountTokenAsync(Audience, ["provider-admin"], ct);

        foreach (var (name, token) in new[]
        {
            ("master realm admin token", await keycloak.MasterAdminTokenAsync(ct)),
            ("provider-admin token with a broken signature", JwtParts.WithBrokenSignature(admin)),
            ("unsigned (alg none) copy of a provider-admin token", JwtParts.Unsigned(admin)),
        })
        {
            using var response = await SendMeAsync(client, token, ct);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"Expected 401 for a {name}, got {(int)response.StatusCode}.");
        }
    }

    [Fact(DisplayName = "Dado o Keycloak real, quando GET /backoffice/me com token de outra audiência, então 401")]
    [Trait("Spec", "SPEC-0009:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenTokenForAnotherAudienceWhenGettingMeThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var token = await keycloak.ServiceAccountTokenAsync("another-api", ["provider-admin"], ct);

        using var response = await SendMeAsync(client, token, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "Dado o Keycloak real, quando GET /backoffice/me com token provider-admin, então 200 com userId e papéis")]
    [Trait("Spec", "SPEC-0009:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenProviderAdminTokenWhenGettingMeThenOk()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var token = await keycloak.ServiceAccountTokenAsync(Audience, ["provider-admin"], ct);

        using var response = await SendMeAsync(client, token, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 for a provider-admin, got {(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        Assert.Equal(JwtParts.Payload(token).GetProperty("sub").GetString(), json.RootElement.GetProperty("userId").GetString());
        var roles = json.RootElement.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        Assert.Contains("provider-admin", roles, StringComparer.Ordinal);
        if (json.RootElement.TryGetProperty("operatorId", out var operatorId))
        {
            Assert.Equal(JsonValueKind.Null, operatorId.ValueKind);
        }
    }

    private static async Task<HttpResponseMessage> SendMeAsync(HttpClient client, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(MePath, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, ct).ConfigureAwait(false);
    }

    private Task<SecurityHost> StartAsync(CancellationToken ct) =>
        SecurityHost.StartAsync(
            postgres.ConnectionString,
            redis.ConnectionString,
            new SecurityHostOptions { BackofficeAuthority = keycloak.Authority, BackofficeAudience = Audience },
            ct);
}
