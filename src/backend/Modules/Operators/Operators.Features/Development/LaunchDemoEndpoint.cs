using System.Security.Cryptography;
using Apogeu.Operators.Features.Configuration;
using Apogeu.Operators.Features.Launch;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Operators.Features.Development;

/// <summary><c>GET /dev/launch-demo</c>, Development only: redirects (302) to a demo launch URL (SPEC-0009 §6).</summary>
internal static class LaunchDemoEndpoint
{
    public const string Route = "/dev/launch-demo";

    /// <summary>Maps the route only in Development: in any other environment it does not exist (404).</summary>
    public static IEndpointRouteBuilder MapLaunchDemo(this IEndpointRouteBuilder endpoints)
    {
        if (!endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return endpoints;
        }

        endpoints
            .MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<RedirectHttpResult> HandleAsync(LaunchIssuer issuer, CancellationToken cancellationToken)
    {
        // A new pseudonymous demo player each time, in the demo sala of the demo operator.
        var subject = new LaunchSubject(
            DemoOperator.Id,
            DemoOperator.SalaId,
            "dev-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)),
            Nickname: null,
            LaunchRequestValidator.SupportedCurrency,
            LaunchRequestValidator.DemoMode,
            LaunchRequestValidator.SupportedLanguage);
        var launch = await issuer.IssueAsync(subject, cancellationToken).ConfigureAwait(false);
        return TypedResults.Redirect(launch.LaunchUrl.AbsoluteUri);
    }
}
