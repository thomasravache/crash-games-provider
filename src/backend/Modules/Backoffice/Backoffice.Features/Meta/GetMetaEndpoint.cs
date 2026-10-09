using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Apogeu.Backoffice.Features.Meta;

/// <summary><c>GET /api/v1/meta</c>: proves the generated-contract cycle end to end (SPEC-0006).</summary>
internal static class GetMetaEndpoint
{
    public const string Route = "/api/v1/meta";

    public static IEndpointRouteBuilder MapGetMeta(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(Route, Handle)
            .WithName("GetMeta")
            .WithTags("Meta");

        return endpoints;
    }

    private static Ok<MetaResponse> Handle(TimeProvider timeProvider) =>
        TypedResults.Ok(new MetaResponse(MetaResponse.Current, timeProvider.GetUtcNow()));
}
