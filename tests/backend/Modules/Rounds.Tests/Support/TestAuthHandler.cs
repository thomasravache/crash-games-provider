using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// Test authentication scheme of the integration hosts (SPEC-0008 §7.6, D6): <c>Authorization: Test &lt;player&gt;</c>
/// authenticates the player; no header, no user. The real player session comes from SPEC-0009.
/// </summary>
internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(SchemeName + " ", StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var player = header[(SchemeName.Length + 1)..];
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, player), new Claim("sala", "demo")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
