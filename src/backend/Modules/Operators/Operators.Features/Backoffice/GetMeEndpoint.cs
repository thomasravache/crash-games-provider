using Apogeu.Operators.Features.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Apogeu.Operators.Features.Backoffice;

/// <summary><c>GET /api/v1/backoffice/me</c> (Keycloak bearer): proves the backoffice identity (SPEC-0009 §6).</summary>
internal static class GetMeEndpoint
{
    public const string Route = "/api/v1/backoffice/me";

    public static IEndpointRouteBuilder MapGetMe(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(Route, Handle)
            .RequireAuthorization(OperatorsAuthentication.BackofficePolicy)
            .WithName("GetBackofficeMe")
            .WithTags("Backoffice")
            .WithSummary("The authenticated backoffice user: id, Apogeu roles and operator.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static Ok<MeResponse> Handle(HttpContext http) =>
        TypedResults.Ok(new MeResponse(
            http.User.FindFirst("sub")?.Value ?? string.Empty,
            BackofficeClaims.RolesOf(http.User),
            BackofficeClaims.OperatorOf(http.User)));
}
