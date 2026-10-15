using Apogeu.DemoCasino.Session;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apogeu.DemoCasino.Lobby;

/// <summary>The lobby endpoint of SPEC-0044 §6: <c>GET /casino/games</c>, for a player with a casino session.</summary>
internal static class LobbyEndpoints
{
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";

    public static IServiceCollection AddLobby(this IServiceCollection services)
    {
        services.AddOptions<LobbyOptions>().BindConfiguration(LobbyOptions.SectionName);
        services.TryAddSingleton(TimeProvider.System);
        services.AddMetrics();
        services.AddSingleton<ApogeuCatalog>();
        return services;
    }

    public static IEndpointRouteBuilder MapLobby(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/casino").MapGet("/games", ListGames);
        return endpoints;
    }

    private static async Task<IResult> ListGames(HttpContext context, CasinoSessionCookie cookie, ApogeuCatalog catalog)
    {
        if (cookie.Read(context) is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            return Results.Json(await catalog.GamesAsync(context.RequestAborted).ConfigureAwait(false));
        }
        catch (ProviderUnavailableException)
        {
            return Results.Json(new ErrorResponse(ProviderUnavailable), statusCode: StatusCodes.Status502BadGateway);
        }
    }

    internal sealed record ErrorResponse(string Code);
}
