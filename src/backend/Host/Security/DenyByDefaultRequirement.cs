using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Host.Security;

/// <summary>
/// The fallback authorization of the Host (ADR-0015: denied by default, each endpoint declares its policy). An endpoint
/// without a policy of its own is allowed only when it is one of the <see cref="PublicRoutes"/> of the Host, or a hub
/// called with a valid player session; anything else is refused (401 without a session, 403 with one). A request that
/// matched no endpoint is let through so the router answers 404.
/// </summary>
internal sealed class DenyByDefaultRequirement : AuthorizationHandler<DenyByDefaultRequirement>, IAuthorizationRequirement
{
    /// <summary>Route patterns the Host publishes anonymously: health probes, the Development OpenAPI document and the API meta.</summary>
    public static readonly IReadOnlySet<string> PublicRoutes = new HashSet<string>(StringComparer.Ordinal)
    {
        "/health",
        "/alive",
        "/openapi/{documentName}.json",
        "/api/v1/meta",
    };

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, DenyByDefaultRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        var endpoint = (context.Resource as Microsoft.AspNetCore.Http.HttpContext)?.GetEndpoint()
            ?? context.Resource as Microsoft.AspNetCore.Http.Endpoint;
        var allowed = endpoint switch
        {
            null => true,
            _ when endpoint.Metadata.GetMetadata<HubMetadata>() is not null => context.User.Identity?.IsAuthenticated == true,
            Microsoft.AspNetCore.Routing.RouteEndpoint route => route.RoutePattern.RawText is { } pattern && PublicRoutes.Contains(pattern),
            _ => false,
        };
        if (allowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
