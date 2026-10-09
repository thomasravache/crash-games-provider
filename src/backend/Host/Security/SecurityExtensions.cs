using Apogeu.Operators.Features.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Apogeu.Host.Security;

/// <summary>
/// Security pipeline of the Host (SPEC-0009, ADR-0015): authorization denied by default (<see cref="DenyByDefaultRequirement"/>),
/// security headers and HSTS on every response, then authentication and authorization. The identities, their schemes and
/// policies come from the Operators module.
/// </summary>
internal static class SecurityExtensions
{
    public static IServiceCollection AddHostSecurity(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, DenyByDefaultRequirement>();
        services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder(OperatorsAuthentication.PlayerSessionScheme)
            .AddRequirements(new DenyByDefaultRequirement())
            .Build());
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        return services;
    }

    /// <summary>Must come before the endpoints: headers first, so even a 401 or a 404 carries them.</summary>
    public static WebApplication UseHostSecurity(this WebApplication app)
    {
        app.UseMiddleware<SecurityHeaders>();
        app.UseHsts();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
