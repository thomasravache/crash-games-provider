using System.Security.Cryptography;
using Apogeu.Operators.Features.Configuration;
using Apogeu.Operators.Features.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Apogeu.Operators.Features.Sessions;

/// <summary>
/// The key that signs player sessions (HMAC-SHA256), from the secret parameter <c>Security:PlayerSession:SigningKey</c>
/// (base64, at least 32 bytes; ADR-0014). Resolved on first use, so the host starts without it: in Development a missing key
/// becomes an ephemeral one of the process; elsewhere signing or validating a session fails until it is configured.
/// </summary>
internal sealed class PlayerSessionSigningKey
{
    public const int MinimumKeyBytes = 32;

    private readonly Lazy<SymmetricSecurityKey> _key;

    public PlayerSessionSigningKey(IOptions<PlayerSessionOptions> options, IHostEnvironment environment, ILogger<PlayerSessionSigningKey> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _key = new Lazy<SymmetricSecurityKey>(() => Create(options.Value.SigningKey, environment, logger), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>The key; throws <see cref="InvalidOperationException"/> when it is missing or invalid outside Development.</summary>
    public SymmetricSecurityKey Key => _key.Value;

    /// <summary>The key for validation: an unusable key validates nothing instead of failing the request.</summary>
    public IEnumerable<SecurityKey> ValidationKeys()
    {
        try
        {
            return [_key.Value];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    private static SymmetricSecurityKey Create(string? configured, IHostEnvironment environment, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"Missing required configuration '{PlayerSessionOptions.SectionName}:SigningKey' (base64 of at least {MinimumKeyBytes} bytes).");
            }

            OperatorsLog.EphemeralSessionKey(logger);
            return new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(MinimumKeyBytes));
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(configured);
        }
        catch (FormatException exception)
        {
            // The message of a FormatException of Convert never carries the input.
            throw new InvalidOperationException($"'{PlayerSessionOptions.SectionName}:SigningKey' must be base64.", exception);
        }

        return bytes.Length >= MinimumKeyBytes
            ? new SymmetricSecurityKey(bytes)
            : throw new InvalidOperationException($"'{PlayerSessionOptions.SectionName}:SigningKey' must have at least {MinimumKeyBytes} bytes.");
    }
}
