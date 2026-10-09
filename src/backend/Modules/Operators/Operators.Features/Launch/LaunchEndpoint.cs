using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.BuildingBlocks.Security;
using Apogeu.Operators.Contracts;
using Apogeu.Operators.Features.Authentication;
using Apogeu.Operators.Features.Errors;
using Apogeu.Operators.Features.Observability;
using Apogeu.Operators.Infrastructure;
using Apogeu.Operators.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apogeu.Operators.Features.Launch;

/// <summary>
/// <c>POST /api/v1/operator/launch</c> (HMAC): issues a launch URL for a player (SPEC-0009 §6), only for a game of the catalog
/// enabled for the operator and a currency the operator has limits in (SPEC-0020 §6).
/// </summary>
internal static class LaunchEndpoint
{
    public const string Route = "/api/v1/operator/launch";

    /// <summary>Launches per minute per API key (SPEC-0009 IT-08).</summary>
    public const int PermitsPerWindow = 60;

    public static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);

    /// <summary>How long a signature is remembered: it is accepted at most ±<see cref="OperatorRequestVerifier.Window"/> from its timestamp.</summary>
    public static readonly TimeSpan ReplayRetention = OperatorRequestVerifier.Window * 2 + TimeSpan.FromMinutes(1);

    public static IEndpointRouteBuilder MapLaunch(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(Route, HandleAsync)
            .RequireAuthorization(OperatorsAuthentication.OperatorPolicy)
            .WithName("LaunchGame")
            .WithTags("Operator")
            .WithSummary("Issues a single-use launch URL for a player (demo mode only).")
            .Accepts<LaunchRequest>("application/json")
            .Produces<LaunchResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest, ErrorResponse.ContentType)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized, ErrorResponse.ContentType)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden, ErrorResponse.ContentType)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict, ErrorResponse.ContentType)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests, ErrorResponse.ContentType);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext http,
        IReplayGuard replays,
        IRateLimiter rateLimiter,
        IOperatorDirectory operators,
        IGameCatalog catalog,
        IOperatorLimits limits,
        LaunchIssuer issuer,
        IOptions<JsonOptions> jsonOptions,
        TimeProvider timeProvider,
        OperatorsMetrics metrics,
        ILogger<LaunchIssuer> logger,
        CancellationToken cancellationToken)
    {
        var signed = http.Features.Get<OperatorSignedBody>()
            ?? throw new InvalidOperationException("The launch endpoint requires the operator signature authentication.");
        var operatorId = http.User.FindFirst(OperatorsAuthentication.OperatorClaim)!.Value;
        var keyId = http.User.FindFirst(OperatorsAuthentication.KeyClaim)!.Value;
        http.Response.Headers.CacheControl = "no-store";

        if (!await replays.TryRegisterAsync(Fingerprint(keyId, signed.Signature), ReplayRetention, cancellationToken).ConfigureAwait(false))
        {
            metrics.ReplayDetected();
            metrics.LaunchRejected(SecurityErrorCodes.ReplayDetected);
            OperatorsLog.ReplayDetected(logger, keyId);
            return SecurityProblems.Of(StatusCodes.Status409Conflict, SecurityErrorCodes.ReplayDetected);
        }

        if (!await rateLimiter.TryAcquireAsync("launch:" + keyId, PermitsPerWindow, RateWindow, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            metrics.RateLimited(Route);
            metrics.LaunchRejected(SecurityErrorCodes.RateLimited);
            OperatorsLog.RateLimited(logger, Route);
            return SecurityProblems.Of(StatusCodes.Status429TooManyRequests, SecurityErrorCodes.RateLimited);
        }

        var request = Deserialize(signed.Body, jsonOptions.Value.SerializerOptions);
        var validation = request is null
            ? new LaunchRequestValidation(SecurityErrorCodes.ValidationError, new Dictionary<string, string[]>(StringComparer.Ordinal) { ["body"] = ["A JSON object is required."] })
            : LaunchRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            var code = validation.ErrorCode!;
            metrics.LaunchRejected(code);
            OperatorsLog.LaunchRejected(logger, code, keyId);
            var status = string.Equals(code, SecurityErrorCodes.RealMoneyDisabled, StringComparison.Ordinal)
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status400BadRequest;
            return SecurityProblems.Of(status, code, validation.Errors);
        }

        if (await operators.FindAsync(operatorId, cancellationToken).ConfigureAwait(false) is not { } registered)
        {
            metrics.LaunchRejected(SecurityErrorCodes.UnknownKey);
            OperatorsLog.OperatorNotRegistered(logger, keyId, operatorId);
            return SecurityProblems.Of(StatusCodes.Status401Unauthorized, SecurityErrorCodes.UnknownKey);
        }

        return await IssueAsync(catalog, limits, issuer, metrics, logger, registered, request!, keyId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Issues the launch when the game and the currency are available to the operator (SPEC-0020 §6).</summary>
    private static async Task<IResult> IssueAsync(
        IGameCatalog catalog,
        IOperatorLimits limits,
        LaunchIssuer issuer,
        OperatorsMetrics metrics,
        ILogger logger,
        OperatorRecord registered,
        LaunchRequest request,
        string keyId,
        CancellationToken cancellationToken)
    {
        if (CatalogRejection(catalog, limits, new OperatorId(registered.Id), request) is { } rejection)
        {
            metrics.LaunchRejected(rejection);
            metrics.CatalogLaunchRejected(rejection);
            OperatorsLog.LaunchRejected(logger, rejection, keyId);
            return SecurityProblems.Of(StatusCodes.Status400BadRequest, rejection);
        }

        var subject = new LaunchSubject(
            registered.Id,
            registered.SalaId,
            request.PlayerId!,
            request.Nickname,
            request.Currency!,
            request.Mode!,
            request.Language!);
        var launch = await issuer.IssueAsync(subject, cancellationToken).ConfigureAwait(false);
        OperatorsLog.LaunchIssued(logger, registered.Id, keyId, registered.SalaId);
        return TypedResults.Ok(launch);
    }

    /// <summary>
    /// <c>GAME_NOT_AVAILABLE</c> when the game is not in the catalog or not enabled for the operator; otherwise
    /// <c>CURRENCY_NOT_ENABLED</c> when the operator has no limits in the currency; <see langword="null"/> when both are available.
    /// </summary>
    private static string? CatalogRejection(IGameCatalog catalog, IOperatorLimits limits, OperatorId op, LaunchRequest request)
    {
        if (!catalog.IsEnabledFor(op, new GameCode(request.GameCode!)))
        {
            return OperatorsErrorCodes.GameNotAvailable;
        }

        try
        {
            _ = limits.Get(op, new Currency(request.Currency!));
            return null;
        }
        catch (CurrencyNotEnabledException error)
        {
            return error.Code;
        }
    }

    private static LaunchRequest? Deserialize(ReadOnlyMemory<byte> body, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<LaunchRequest>(body.Span, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The replay key: a hash of the key id and the signature, so the store never holds a signature.</summary>
    private static string Fingerprint(string keyId, string signature) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(keyId + "\n" + signature)));
}
