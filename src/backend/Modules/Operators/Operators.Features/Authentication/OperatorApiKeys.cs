using System.Text;
using Apogeu.Operators.Features.Configuration;
using Microsoft.Extensions.Options;

namespace Apogeu.Operators.Features.Authentication;

/// <summary>
/// The active operator API keys, from secret configuration (ADR-0014: user secrets / Aspire parameters locally, the cloud
/// secret manager elsewhere). Several keys per operator are active at once, so a key is rotated without downtime; a key is
/// revoked by removing it from the configuration.
/// </summary>
internal sealed class OperatorApiKeys
{
    private readonly Dictionary<string, OperatorApiKey> _keys = new(StringComparer.Ordinal);

    public OperatorApiKeys(IOptions<OperatorsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var key in options.Value.Demo.ApiKeys)
        {
            if (!string.IsNullOrWhiteSpace(key.Id) && !string.IsNullOrEmpty(key.Secret))
            {
                _keys[key.Id] = new OperatorApiKey(key.Id, DemoOperator.Id, Encoding.UTF8.GetBytes(key.Secret));
            }
        }
    }

    /// <summary>The active key <paramref name="keyId"/>, or <see langword="null"/> when it is unknown or revoked.</summary>
    public OperatorApiKey? Find(string keyId) => _keys.GetValueOrDefault(keyId);
}
