using Apogeu.Operators.Features.Launch;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Apogeu.Operators.Features.Sessions;

/// <summary>
/// Issues and describes the validation of player sessions (SPEC-0009 §4): a JWT signed by the provider with HS256, valid for
/// <see cref="PlayerSessionClaims.Lifetime"/>, with the claims <c>sub</c>, <c>op</c>, <c>sala</c>, <c>cur</c> and
/// <c>mode</c>. Times come from the injected clock.
/// </summary>
internal sealed class PlayerSessionTokens(PlayerSessionSigningKey signingKey, TimeProvider timeProvider)
{
    public const string Issuer = "apogeu";

    public const string Audience = "apogeu-game";

    /// <summary>Tolerance between the clocks of the instances of the API.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    /// <summary>A session for <paramref name="subject"/> issued at <paramref name="issuedAt"/>, and its expiry.</summary>
    public (string Token, DateTimeOffset ExpiresAt) Issue(LaunchSubject subject, DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var expiresAt = issuedAt + PlayerSessionClaims.Lifetime;
        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [PlayerSessionClaims.Subject] = subject.PlayerId,
                [PlayerSessionClaims.Operator] = subject.OperatorId,
                [PlayerSessionClaims.Sala] = subject.SalaId,
                [PlayerSessionClaims.Currency] = subject.Currency,
                [PlayerSessionClaims.Mode] = subject.Mode,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            },
            SigningCredentials = new SigningCredentials(signingKey.Key, SecurityAlgorithms.HmacSha256),
        });
        return (token, expiresAt);
    }

    /// <summary>Validation of a session: signature with the provider key, issuer, audience and lifetime on the injected clock.</summary>
    public void Configure(TokenValidationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        parameters.ValidIssuer = Issuer;
        parameters.ValidAudience = Audience;
        parameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
        parameters.RequireSignedTokens = true;
        parameters.RequireExpirationTime = true;
        parameters.ValidateIssuerSigningKey = true;
        parameters.ClockSkew = ClockSkew;
        parameters.NameClaimType = PlayerSessionClaims.Subject;

        // Resolved per token, so a host without the key starts and only rejects sessions (PlayerSessionSigningKey).
        parameters.IssuerSigningKeyResolver = (_, _, _, _) => signingKey.ValidationKeys();
        parameters.LifetimeValidator = (notBefore, expires, _, _) =>
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            return expires is { } exp && now < exp + ClockSkew && (notBefore is not { } nbf || nbf <= now + ClockSkew);
        };
    }
}
