using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Microsoft.Extensions.Configuration;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>
/// Provisional <see cref="IOperatorWalletSettings"/> (SPEC-0046 Emenda, until the operator registry of SPEC-0038): read from
/// the configuration <c>Wallet:Operators:{op}:Mode</c> (<c>Demo</c>|<c>Seamless</c>), <c>WalletUrl</c>, <c>KeyId</c> and
/// <c>Secret</c> (environment variable or secret store, never the repository, ADR-0014). An operator without configuration
/// is demo; a seamless one without URL or key stays seamless and its calls are refused (never silently demo).
/// </summary>
internal sealed class ConfigurationOperatorWalletSettings(IConfiguration? configuration) : IOperatorWalletSettings
{
    public const string SectionName = "Wallet:Operators";

    private static readonly OperatorWalletSettings Demo = new(WalletMode.Demo, WalletUrl: null, Key: null);

    public Task<OperatorWalletSettings> GetAsync(OperatorId op, CancellationToken ct) => Task.FromResult(Read(op));

    private OperatorWalletSettings Read(OperatorId op)
    {
        if (configuration is null || string.IsNullOrEmpty(op.Value))
        {
            return Demo;
        }

        var section = configuration.GetSection(SectionName).GetSection(op.Value);
        if (!Enum.TryParse<WalletMode>(section["Mode"], ignoreCase: true, out var mode) || mode != WalletMode.Seamless)
        {
            return Demo;
        }

        var url = Uri.TryCreate(section["WalletUrl"], UriKind.Absolute, out var parsed)
            && (string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
            ? parsed
            : null;
        var keyId = section["KeyId"];
        var secret = section["Secret"];
        var key = string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(secret) ? null : new OperatorWalletKey(keyId, secret);
        return new OperatorWalletSettings(WalletMode.Seamless, url, key);
    }
}
