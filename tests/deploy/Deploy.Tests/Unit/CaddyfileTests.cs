using System.Text.Json.Nodes;
using Apogeu.Deploy.Tests.Support;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Deploy.Tests.Unit;

/// <summary>
/// SPEC-0066 UT-01: infra/caddy/Caddyfile under <c>caddy validate</c> of the image caddy:2.11.7 and inspected through
/// <c>caddy adapt</c> (the JSON Caddy really runs): the three sites of the contract on the base domain of
/// <c>APOGEU_DOMAIN</c> (default 143-95-218-240.sslip.io), <c>/casino/*</c> of the casino to demo-casino:8080, the api
/// to api:8080, static files with SPA fallback for the casino and the game, automatic HTTPS with the HTTP redirect on,
/// and the security headers on every site. Emenda v3: the X-Forwarded-* headers that come from the internet are dropped
/// and rewritten by the proxy (no trusted_proxies; the api trusts X-Forwarded-*), shown through the real Caddyfile in
/// front of an echo backend. Emenda v4: the access log of the proxy keeps no token: the launch token <c>lt</c> of the game
/// and the <c>access_token</c> of the SignalR hubs are erased from the logged query, as Cookie and Authorization.
/// </summary>
public sealed class CaddyfileTests
{
    private const string PublicDomain = "143-95-218-240.sslip.io";
    private const string TestDomain = "ut01.localhost";

    [Fact(DisplayName = "Dado o Caddyfile com o host de teste, quando validado pelo caddy validate 2.11.7, então a configuração é válida")]
    [Trait("Category", "Unit")]
    [Trait("Spec", "SPEC-0066:UT-01")]
    public async Task GivenTheCaddyfileWithTheTestHostWhenValidatedThenItIsValid()
    {
        var result = await CaddyAsync(["validate", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"], TestDomain);

        Assert.True(result.Succeeded, "caddy validate rejected infra/caddy/Caddyfile. " + result.Describe());
        Assert.Contains("Valid configuration", result.Output, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "Dado o Caddyfile, quando adaptado, então só existem os três sites, com as rotas, o HTTPS e os cabeçalhos do contrato")]
    [Trait("Category", "Unit")]
    [Trait("Spec", "SPEC-0066:UT-01")]
    [InlineData(TestDomain)]
    [InlineData(null)]
    public async Task GivenTheCaddyfileWhenAdaptedThenItHasTheThreeSitesOfTheContract(string? domain)
    {
        var baseDomain = domain ?? PublicDomain;
        var config = await AdaptAsync(domain);
        var servers = config["apps"]?["http"]?["servers"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException("caddy adapt produced no apps.http.servers: " + config.ToJsonString());

        // HTTP → HTTPS: automatic HTTPS (certificates and the redirect of port 80) is never turned off.
        foreach (var (name, server) in servers)
        {
            var automatic = server?["automatic_https"];
            Assert.False(automatic?["disable"]?.GetValue<bool>() ?? false, $"server {name}: automatic HTTPS disabled");
            Assert.False(automatic?["disable_redirects"]?.GetValue<bool>() ?? false, $"server {name}: HTTP→HTTPS redirect disabled");
        }

        var sites = servers
            .SelectMany(server => server.Value?["routes"]?.AsArray() ?? [])
            .Select(route => (Hosts: HostsOf(route), Route: route!))
            .ToList();
        Assert.All(sites, site => Assert.NotEmpty(site.Hosts));
        var hosts = sites.SelectMany(site => site.Hosts).Order(StringComparer.Ordinal).ToArray();
        string[] expected = [$"api.{baseDomain}", $"cassino.{baseDomain}", $"jogo.{baseDomain}"];
        Assert.Equal(expected, hosts);

        var casino = SiteOf(sites, $"cassino.{baseDomain}");
        var game = SiteOf(sites, $"jogo.{baseDomain}");
        var api = SiteOf(sites, $"api.{baseDomain}");

        // cassino: /casino/* → demo-casino:8080; the rest, static files of the demo-site with fallback to index.html.
        var casinoRoutes = Nodes(casino)
            .Where(node => node["match"] is JsonArray match && match.Any(m => (m?["path"]?.AsArray() ?? []).Any(p => p?.GetValue<string>() is "/casino/*" or "/casino*")))
            .ToList();
        Assert.True(casinoRoutes.Count > 0, "cassino: no route matching /casino/*. " + casino.ToJsonString());
        Assert.Contains("demo-casino:8080", casinoRoutes.SelectMany(Dials), StringComparer.Ordinal);
        Assert.DoesNotContain("api:8080", Dials(casino), StringComparer.Ordinal);
        AssertStaticWithSpaFallback(casino, "cassino");

        // jogo: static files of game-pipa with fallback to index.html, nothing proxied.
        AssertStaticWithSpaFallback(game, "jogo");
        Assert.Empty(Dials(game));

        // api: everything to api:8080 (REST and the /hubs WebSocket: reverse_proxy upgrades connections by itself).
        Assert.Equal<string>(["api:8080"], [.. Dials(api).Distinct(StringComparer.Ordinal)]);
        Assert.DoesNotContain(Nodes(api), node => IsHandler(node, "file_server"));

        foreach (var (host, site) in new[] { ("cassino", casino), ("jogo", game), ("api", api) })
        {
            AssertSecurityHeaders(site, host);
        }
    }

    [Fact(DisplayName = "Dado X-Forwarded-* enviados pelo cliente, quando passam pelo proxy, então chegam à api e ao demo-casino descartados e reescritos")]
    [Trait("Category", "Unit")]
    [Trait("Spec", "SPEC-0066:UT-01")]
    public async Task GivenForwardedHeadersFromTheClientWhenProxiedThenTheBackendGetsTheProxyValues()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var config = await AdaptAsync(TestDomain);
        Assert.DoesNotContain(Nodes(config), node => node.ContainsKey("trusted_proxies"));

        await using var proxy = await ProxyProbe.StartAsync(DeployScripts.PathOf(DeployScripts.Caddyfile), TestDomain, cancellationToken);
        foreach (var (site, path) in new[] { ("api", "/echo.cgi"), ("cassino", "/casino/echo.cgi") })
        {
            var host = $"{site}.{TestDomain}";
            var seen = await proxy.EchoAsync(host, path, cancellationToken);

            Assert.True(seen.TryGetValue("XFF", out var forwardedFor) && forwardedFor.Length > 0, $"{host}: no X-Forwarded-For at the backend");
            Assert.DoesNotContain(ProxyProbe.ForgedFor, forwardedFor, StringComparison.Ordinal);
            Assert.True(
                seen.TryGetValue("XFH", out var forwardedHost) && forwardedHost.StartsWith(host, StringComparison.Ordinal),
                $"{host}: X-Forwarded-Host at the backend is '{forwardedHost}', not the host of the request");
            Assert.Equal("https", seen.GetValueOrDefault("XFP"));
        }
    }

    [Fact(DisplayName = "Dados tokens na query (lt, access_token), cookie e Authorization, quando passam pelo proxy, então o log de acesso não os grava")]
    [Trait("Category", "Unit")]
    [Trait("Spec", "SPEC-0066:UT-01")]
    public async Task GivenTokensInTheRequestWhenProxiedThenTheAccessLogDoesNotKeepThem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string launchToken = "LT-SEGREDO-5d1f0c";
        const string accessToken = "AT-SEGREDO-9b7e22";
        const string cookie = "CK-SEGREDO-41aa07";
        const string authorization = "AZ-SEGREDO-c3e815";
        string[] headers = ["Cookie: casino_session=" + cookie, "Authorization: Bearer " + authorization];

        await using var proxy = await ProxyProbe.StartAsync(DeployScripts.PathOf(DeployScripts.Caddyfile), TestDomain, cancellationToken);
        await proxy.GetAsync($"jogo.{TestDomain}", $"/?lt={launchToken}&lang=pt-BR", headers, cancellationToken);
        await proxy.GetAsync($"api.{TestDomain}", $"/hubs/x?id=abc&access_token={accessToken}", headers, cancellationToken);
        await proxy.GetAsync($"cassino.{TestDomain}", $"/casino/echo.cgi?lt={launchToken}", headers, cancellationToken);

        // The requests are in the access log (the test is not passing on an empty log) …
        string logs = string.Empty;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && !logs.Contains("/hubs/x", StringComparison.Ordinal))
        {
            await Task.Delay(250, cancellationToken);
            logs = await proxy.LogsAsync(cancellationToken);
        }

        Assert.Contains("/hubs/x", logs, StringComparison.Ordinal);
        Assert.Contains("lang=pt-BR", logs, StringComparison.Ordinal);

        // … and none of the secrets.
        foreach (var secret in new[] { launchToken, accessToken, cookie, authorization })
        {
            Assert.False(logs.Contains(secret, StringComparison.Ordinal), $"the access log of the proxy keeps {secret}:\n{logs}");
        }
    }

    private static void AssertStaticWithSpaFallback(JsonNode site, string host)
    {
        var nodes = Nodes(site).ToList();
        Assert.True(
            nodes.Any(node => IsHandler(node, "file_server")),
            $"{host}: no file_server for the static front-end. " + site.ToJsonString());
        Assert.True(
            nodes.Any(node => (node["file"]?["try_files"]?.AsArray() ?? []).Any(file => string.Equals(file?.GetValue<string>(), "/index.html", StringComparison.Ordinal))),
            $"{host}: no SPA fallback (try_files … /index.html). " + site.ToJsonString());
    }

    private static void AssertSecurityHeaders(JsonNode site, string host)
    {
        var set = Nodes(site)
            .Where(node => IsHandler(node, "headers"))
            .Select(node => node["response"]?["set"]?.AsObject())
            .OfType<JsonObject>()
            .SelectMany(headers => headers)
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => string.Join(' ', group.SelectMany(header => header.Value?.AsArray() ?? []).Select(value => value?.GetValue<string>())),
                StringComparer.OrdinalIgnoreCase);

        Assert.True(set.TryGetValue("Strict-Transport-Security", out var hsts), $"{host}: no Strict-Transport-Security header");
        Assert.Contains("max-age=", hsts, StringComparison.OrdinalIgnoreCase);
        Assert.True(set.TryGetValue("X-Content-Type-Options", out var nosniff), $"{host}: no X-Content-Type-Options header");
        Assert.Equal("nosniff", nosniff, ignoreCase: true);
        Assert.True(set.TryGetValue("Referrer-Policy", out var referrer) && referrer.Length > 0, $"{host}: no Referrer-Policy header");
    }

    private static JsonNode SiteOf(List<(List<string> Hosts, JsonNode Route)> sites, string host) =>
        sites.Single(site => site.Hosts.Contains(host, StringComparer.Ordinal)).Route;

    private static bool IsHandler(JsonObject node, string handler) =>
        string.Equals(node["handler"]?.GetValue<string>(), handler, StringComparison.Ordinal);

    private static List<string> HostsOf(JsonNode? route) =>
        [.. (route?["match"]?.AsArray() ?? []).SelectMany(match => match?["host"]?.AsArray() ?? []).Select(host => host!.GetValue<string>())];

    /// <summary>Upstreams (dial) of every reverse_proxy under a node.</summary>
    private static List<string> Dials(JsonNode node) =>
        [.. Nodes(node)
            .Where(child => IsHandler(child, "reverse_proxy"))
            .SelectMany(proxy => proxy["upstreams"]?.AsArray() ?? [])
            .Select(upstream => upstream?["dial"]?.GetValue<string>() ?? string.Empty)];

    /// <summary>The node and every object below it.</summary>
    private static IEnumerable<JsonObject> Nodes(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                yield return obj;
                foreach (var (_, child) in obj)
                {
                    foreach (var nested in Nodes(child))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    foreach (var nested in Nodes(item))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static async Task<JsonNode> AdaptAsync(string? domain)
    {
        var result = await CaddyAsync(["adapt", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"], domain).ConfigureAwait(false);
        Assert.True(result.Succeeded, "caddy adapt failed on infra/caddy/Caddyfile. " + result.Describe());
        return JsonNode.Parse(result.StandardOutput)
            ?? throw new Xunit.Sdk.XunitException("caddy adapt printed no JSON: " + result.Describe());
    }

    /// <summary>Runs the caddy CLI of the image caddy:2.11.7 on the Caddyfile of the working tree, mounted read-only.</summary>
    private static async Task<ProcessResult> CaddyAsync(IEnumerable<string> arguments, string? domain)
    {
        var caddyfile = DeployScripts.PathOf(DeployScripts.Caddyfile);
        Assert.True(File.Exists(caddyfile), $"{DeployScripts.Caddyfile} does not exist");

        string[] environment = domain is null ? [] : ["--env", $"{DeployScripts.DomainVariable}={domain}"];
        return await DockerCli.RunAsync(
            ["run", "--rm", "--network", "none", .. environment, "--volume", $"{caddyfile}:/etc/caddy/Caddyfile:ro", DockerCli.CaddyImage, "caddy", .. arguments],
            TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}
