using System.Text.Json;
using Apogeu.BuildingBlocks.Security;
using Apogeu.Operators.Features.Errors;
using Apogeu.Operators.Features.Launch;
using Apogeu.Operators.Features.Observability;
using Apogeu.Operators.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apogeu.Operators.Features.Sessions;

/// <summary>
/// <c>POST /api/v1/sessions</c>: exchanges a launch token for a player session (SPEC-0009 §6). Public by design: the
/// single-use launch token is the credential.
/// </summary>
internal static class CreateSessionEndpoint
{
    public const string Route = "/api/v1/sessions";

    /// <summary>Exchanges per minute per client address.</summary>
    public const int PermitsPerWindow = 120;

    public static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);

    private const int MaxBodyBytes = 4 * 1024;

    public static IEndpointRouteBuilder MapCreateSession(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(Route, HandleAsync)
            .AllowAnonymous()
            .WithName("CreateSession")
            .WithTags("Sessions")
            .WithSummary("Exchanges a launch token for a player session.")
            .Accepts<CreateSessionRequest>("application/json")
            .Produces<SessionResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized, ErrorResponse.ContentType)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests, ErrorResponse.ContentType);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext http,
        ILaunchGrantStore grants,
        IRateLimiter rateLimiter,
        PlayerSessionTokens sessions,
        IOptions<JsonOptions> jsonOptions,
        TimeProvider timeProvider,
        OperatorsMetrics metrics,
        ILogger<PlayerSessionTokens> logger,
        CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = "no-store";

        // Per client address; the address is the partition only, it is never logged (ADR-0014).
        var partition = "session:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        if (!await rateLimiter.TryAcquireAsync(partition, PermitsPerWindow, RateWindow, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            metrics.RateLimited(Route);
            OperatorsLog.RateLimited(logger, Route);
            return SecurityProblems.Of(StatusCodes.Status429TooManyRequests, SecurityErrorCodes.RateLimited);
        }

        var presented = await ReadLaunchTokenAsync(http.Request, jsonOptions.Value.SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(presented))
        {
            return Refuse(logger, SecurityErrorCodes.LaunchTokenInvalid);
        }

        var hash = LaunchToken.HashOf(presented);
        if (await grants.FindAsync(hash, cancellationToken).ConfigureAwait(false) is not { } record)
        {
            return Refuse(logger, SecurityErrorCodes.LaunchTokenInvalid);
        }

        var subject = new LaunchSubject(record.OperatorId, record.SalaId, record.PlayerId, record.Nickname, record.Currency, record.Mode, record.Language);
        var grant = LaunchGrant.Restore(record.TokenHash, subject, record.IssuedAt, record.ExpiresAt, record.RedeemedAt is not null);
        var now = timeProvider.GetUtcNow();
        var redemption = grant.Redeem(now);
        if (redemption == LaunchRedemption.Redeemed && !await grants.TryMarkRedeemedAsync(hash, now, cancellationToken).ConfigureAwait(false))
        {
            // Another request redeemed it between the read and the update.
            redemption = LaunchRedemption.AlreadyUsed;
        }

        switch (redemption)
        {
            case LaunchRedemption.AlreadyUsed:
                return Refuse(logger, SecurityErrorCodes.LaunchTokenUsed);
            case LaunchRedemption.Expired:
                return Refuse(logger, SecurityErrorCodes.LaunchTokenExpired);
        }

        var (token, expiresAt) = sessions.Issue(subject, now);
        OperatorsLog.SessionOpened(logger, subject.OperatorId, subject.SalaId);
        return TypedResults.Ok(new SessionResponse(token, expiresAt, new SessionPlayer(subject.Nickname), subject.SalaId, subject.Currency, subject.Mode));
    }

    private static JsonHttpResult<ErrorResponse> Refuse(ILogger logger, string code)
    {
        OperatorsLog.SessionRefused(logger, code);
        return SecurityProblems.Of(StatusCodes.Status401Unauthorized, code);
    }

    private static async Task<string?> ReadLaunchTokenAsync(HttpRequest request, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes)
        {
            return null;
        }

        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = MaxBodyBytes;
        }

        try
        {
            var body = await JsonSerializer.DeserializeAsync<CreateSessionRequest>(request.Body, options, cancellationToken).ConfigureAwait(false);
            return body?.LaunchToken;
        }
        catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
        {
            return null;
        }
    }
}
