using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Apogeu.BuildingBlocks.Security;
using Apogeu.Operators.Features.Errors;
using Apogeu.Operators.Features.Observability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apogeu.Operators.Features.Authentication;

/// <summary>
/// Authenticates operator systems by the HMAC signature of SPEC-0009 §4: the key must be active, the timestamp inside the
/// ±300 s window of the injected clock and the signature must cover the timestamp, method, path and body. A rejected request
/// is challenged with <c>401</c> and the contract code (<c>UNKNOWN_KEY</c>, <c>TIMESTAMP_OUT_OF_WINDOW</c>,
/// <c>INVALID_SIGNATURE</c>). Replays and rate limits are checked by the endpoint, after authentication.
/// </summary>
internal sealed class OperatorSignatureAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    OperatorApiKeys keys,
    OperatorRequestVerifier verifier,
    OperatorsMetrics metrics)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    /// <summary>Largest body that is read to verify a signature; operator requests are small JSON documents.</summary>
    public const int MaxBodyBytes = 64 * 1024;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var keyId = Single(OperatorSignatureHeaders.KeyId);
        if (keyId is null || keys.Find(keyId) is not { } key)
        {
            return Reject(SecurityErrorCodes.UnknownKey, keyId: null);
        }

        if (!long.TryParse(Single(OperatorSignatureHeaders.Timestamp), NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
        {
            return Reject(SecurityErrorCodes.TimestampOutOfWindow, key.Id);
        }

        var signature = Single(OperatorSignatureHeaders.Signature);
        var body = await ReadBodyAsync().ConfigureAwait(false);
        if (signature is null || body is null)
        {
            return Reject(SecurityErrorCodes.InvalidSignature, key.Id);
        }

        var request = new OperatorSignedRequest(timestamp, Request.Method, Request.PathBase.Add(Request.Path).Value ?? string.Empty, body, signature);
        if (OperatorRequestVerifier.ErrorCodeOf(verifier.Verify(request, key.Secret)) is { } code)
        {
            return Reject(code, key.Id);
        }

        // Hex is case-insensitive, so the same signature has several spellings: the replay check must see one canonical
        // form (lowercase hex of the verified bytes), or re-sending it in another case would bypass it.
        Context.Features.Set(new OperatorSignedBody(body, Convert.ToHexStringLower(Convert.FromHexString(signature))));
        var identity = new ClaimsIdentity(
            [
                new Claim(OperatorsAuthentication.OperatorClaim, key.OperatorId),
                new Claim(OperatorsAuthentication.KeyClaim, key.Id),
            ],
            Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var code = Context.Features.Get<OperatorSignatureFailure>()?.Code ?? SecurityErrorCodes.UnknownKey;
        var json = Context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(
            SecurityProblems.Body(StatusCodes.Status401Unauthorized, code),
            json,
            ErrorResponse.ContentType,
            Context.RequestAborted).ConfigureAwait(false);
    }

    private AuthenticateResult Reject(string code, string? keyId)
    {
        Context.Features.Set(new OperatorSignatureFailure(code));
        metrics.LaunchRejected(code);
        OperatorsLog.LaunchRejected(Logger, code, keyId);
        return AuthenticateResult.Fail(code);
    }

    private string? Single(string header) =>
        Request.Headers.TryGetValue(header, out var values) && values.Count == 1 && !string.IsNullOrEmpty(values[0]) ? values[0] : null;

    /// <summary>The whole body (at most <see cref="MaxBodyBytes"/>), or <see langword="null"/> when it is larger.</summary>
    private async Task<byte[]?> ReadBodyAsync()
    {
        if (Request.ContentLength > MaxBodyBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await Request.Body.ReadAsync(chunk, Context.RequestAborted).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private sealed record OperatorSignatureFailure(string Code);
}
