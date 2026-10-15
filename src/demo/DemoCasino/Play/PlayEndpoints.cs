using Apogeu.DemoCasino.Lobby;
using Apogeu.DemoCasino.Session;

namespace Apogeu.DemoCasino.Play;

/// <summary>
/// The launch endpoint of SPEC-0045 §6: <c>POST /casino/play/{gameCode}</c>, for a player with a casino session. The
/// browser gets only the single-use launch URL; the signed call to the Apogeu stays on the server (ADR-0027).
/// </summary>
internal static class PlayEndpoints
{
    public static IServiceCollection AddPlay(this IServiceCollection services)
    {
        services.AddMetrics();
        services.AddSingleton<ApogeuLauncher>();
        return services;
    }

    public static IEndpointRouteBuilder MapPlay(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/casino").MapPost("/play/{gameCode}", Play);
        return endpoints;
    }

    private static async Task<IResult> Play(string gameCode, HttpContext context, CasinoSessionCookie cookie, ApogeuLauncher launcher)
    {
        if (cookie.Read(context) is not { } player)
        {
            return Results.Unauthorized();
        }

        try
        {
            var launchUrl = await launcher.LaunchAsync(player, gameCode, context.RequestAborted).ConfigureAwait(false);
            return Results.Json(new PlayResponse(launchUrl.AbsoluteUri));
        }
        catch (GameNotAvailableException)
        {
            return Results.Json(new ErrorResponse(ApogeuLauncher.GameNotAvailable), statusCode: StatusCodes.Status404NotFound);
        }
        catch (ProviderUnavailableException)
        {
            return Results.Json(new ErrorResponse(LobbyEndpoints.ProviderUnavailable), statusCode: StatusCodes.Status502BadGateway);
        }
    }

    internal sealed record PlayResponse(string LaunchUrl);

    internal sealed record ErrorResponse(string Code);
}
