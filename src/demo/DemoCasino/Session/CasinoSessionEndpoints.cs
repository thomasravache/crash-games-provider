using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apogeu.DemoCasino.Session;

/// <summary>The casino session endpoints of SPEC-0043 §6: enter, who am I, leave.</summary>
internal static partial class CasinoSessionEndpoints
{
    public static IServiceCollection AddCasinoSession(this IServiceCollection services)
    {
        services.AddDataProtection().SetApplicationName("Apogeu.DemoCasino");
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CasinoSessionCookie>();
        return services;
    }

    public static IEndpointRouteBuilder MapCasinoSession(this IEndpointRouteBuilder endpoints)
    {
        var casino = endpoints.MapGroup("/casino");
        casino.MapPost("/session", Enter);
        casino.MapGet("/me", Me);
        casino.MapPost("/logout", Logout);
        return endpoints;
    }

    private static IResult Enter(SessionRequest request, HttpContext context, CasinoSessionCookie cookie, ILoggerFactory loggers)
    {
        var error = SessionEntry.Validate(request.Nickname, request.Currency);
        if (error is not null)
        {
            return Results.Json(new ErrorResponse(error), statusCode: StatusCodes.Status400BadRequest);
        }

        var player = new CasinoPlayer(SessionEntry.NewPlayerId(), request.Nickname!, SessionEntry.SupportedCurrency);
        cookie.Write(context, player);
        var logger = loggers.CreateLogger(LogCategory);
        LogSessionCreated(logger, player.PlayerId);
        return Results.Json(new SessionResponse(player.PlayerId, player.Nickname));
    }

    private static IResult Me(HttpContext context, CasinoSessionCookie cookie) =>
        cookie.Read(context) is { } player
            ? Results.Json(new MeResponse(player.PlayerId, player.Nickname, player.Currency))
            : Results.Unauthorized();

    private static IResult Logout(HttpContext context)
    {
        CasinoSessionCookie.Delete(context);
        return Results.NoContent();
    }

    private const string LogCategory = "Apogeu.DemoCasino.Session";

    [LoggerMessage(Level = LogLevel.Information, Message = "Casino session created. PlayerId={PlayerId}")]
    private static partial void LogSessionCreated(ILogger logger, string playerId);

    internal sealed record SessionRequest(string? Nickname, string? Currency);

    internal sealed record SessionResponse(string PlayerId, string Nickname);

    internal sealed record MeResponse(string PlayerId, string Nickname, string Currency);

    internal sealed record ErrorResponse(string Code);
}
