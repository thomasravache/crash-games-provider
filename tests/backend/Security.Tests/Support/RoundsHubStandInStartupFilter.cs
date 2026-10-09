using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Security.Tests.Support;

/// <summary>
/// Maps <see cref="RoundsHubStandIn"/> after the Host's own pipeline, with the authentication and authorization the Host
/// registered (schemes, default and fallback policies, global hub filters), so the stand-in is protected exactly as a hub
/// of the Host would be. Nothing of its own is added: without SPEC-0009's registrations the hub is unprotected and IT-05 fails.
/// </summary>
internal sealed class RoundsHubStandInStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        if (app.ApplicationServices.GetService<IAuthenticationSchemeProvider>() is not null)
        {
            app.UseAuthentication();
        }

        if (app.ApplicationServices.GetService<IAuthorizationPolicyProvider>() is not null)
        {
            app.UseAuthorization();
        }

        app.UseEndpoints(endpoints => endpoints.MapHub<RoundsHubStandIn>(RoundsHubStandIn.Path));
    };
}
