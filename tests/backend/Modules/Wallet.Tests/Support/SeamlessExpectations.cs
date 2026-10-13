using System.Globalization;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Security.Tests.Support;
using Apogeu.Wallet.Contracts;
using Xunit;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>
/// The values of the public operator wallet API (docs/integration/operator-wallet-api.md, SPEC-0021@4) and of SPEC-0046 §6,
/// written here independently of the production code: the paths, the signature headers and the seamless test operator.
/// </summary>
internal static class SeamlessExpectations
{
    /// <summary>Operator of the tests whose wallet is the operator test server (seamless mode).</summary>
    public static readonly OperatorId Casino = new("casino-it");

    public const string BalancePath = "/apogeu/v1/balance";
    public const string DebitPath = "/apogeu/v1/debit";
    public const string CreditPath = "/apogeu/v1/credit";
    public const string RollbackPath = "/apogeu/v1/rollback";

    public const string KeyHeader = "X-Apogeu-Key";
    public const string TimestampHeader = "X-Apogeu-Timestamp";
    public const string SignatureHeader = "X-Apogeu-Signature";

    /// <summary>Clock window the operator accepts (public contract §3).</summary>
    public static readonly TimeSpan TimestampWindow = TimeSpan.FromSeconds(300);

    /// <summary>Deadline of a debit (SPEC-0046 §2).</summary>
    public static readonly TimeSpan DebitDeadline = TimeSpan.FromSeconds(2);

    public static PlayerRef NewCasinoPlayer() => new(Casino, new PlayerId("p-" + Guid.NewGuid().ToString("N")));

    /// <summary>The settings double with <see cref="Casino"/> seamless on <paramref name="stub"/>, signed with <paramref name="key"/>.</summary>
    public static FixedOperatorWalletSettings SeamlessOn(OperatorWalletStub stub, TestApiKey key)
    {
        ArgumentNullException.ThrowIfNull(stub);
        return new FixedOperatorWalletSettings().UseSeamless(Casino, stub.BaseUrl, key);
    }

    /// <summary>
    /// Asserts the request carries the headers of the public contract §3 with the key <paramref name="key"/>, a timestamp
    /// within ±300 s of now and the signature of the reference formula over the exact bytes received.
    /// </summary>
    public static void AssertSigned(StubRequest request, TestApiKey key)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(key);

        Assert.Equal(key.Id, request.KeyId);
        Assert.False(string.IsNullOrEmpty(request.Timestamp), $"Missing {TimestampHeader} on {request.Path}.");
        Assert.True(
            long.TryParse(request.Timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp),
            $"{TimestampHeader} must be Unix seconds, got '{request.Timestamp}'.");
        var signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        Assert.True(
            (request.ReceivedAt - signedAt).Duration() <= TimestampWindow,
            $"{TimestampHeader} {signedAt:O} is outside ±300 s of the reception {request.ReceivedAt:O}.");

        var expected = ReferenceSignature.Compute(key.SecretBytes, timestamp, request.Method, request.Path, request.Body);
        Assert.True(
            string.Equals(expected, request.Signature, StringComparison.Ordinal),
            $"{SignatureHeader} of {request.Method} {request.Path} does not match the reference signature of the public contract (got '{request.Signature}').");
    }
}
