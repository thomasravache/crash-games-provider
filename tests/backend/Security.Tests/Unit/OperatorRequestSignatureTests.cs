using System.Text;
using Apogeu.BuildingBlocks.Security;
using Xunit;

namespace Apogeu.Security.Tests.Unit;

/// <summary>
/// UT-01: the operator signature of SPEC-0009 §4. The reference vector was computed outside .NET (Python <c>hmac</c> +
/// <c>hashlib</c>) from the formula of the spec, so the test does not compare the implementation with itself.
/// </summary>
public sealed class OperatorRequestSignatureTests
{
    private const long Timestamp = 1_767_225_600; // 2026-01-01T00:00:00Z (TestClock.Epoch)
    private const string Method = "POST";
    private const string Path = "/api/v1/operator/launch";
    private const string Body = """{"playerId":"player-ut01","currency":"BRL","gameCode":"pipa","mode":"demo","language":"pt-BR"}""";

    // HMAC-SHA256("apogeu-ut01-reference-key", "1767225600\nPOST\n/api/v1/operator/launch\n" + hex(SHA256(Body))), lowercase hex.
    private const string ReferenceVector = "3e303a26e5289d8e2cf98ede9311400b899fa2a5565eb47277829075bd812319";

    // Same, with the third byte of the body XOR 1 (one byte changed).
    private const string ReferenceVectorWithOneByteChanged = "5a7daa913d1a43f2977ab94454b559ec06b64ccdef1fda28119e1a7594cde65a";

    // Same key and timestamp, GET with an empty body (hex(SHA256("")) still enters the message).
    private const string ReferenceVectorEmptyBody = "5bd4c9f9848e10428bfea617d4a2ce388c3962e5156718a50d9f3aeadf9d7156";

    private static readonly byte[] KeyMaterial = Encoding.UTF8.GetBytes("apogeu-ut01-reference-key");

    [Fact(DisplayName = "Dados segredo, timestamp, método, caminho e corpo fixos, quando a assinatura é calculada, então é igual ao vetor de referência")]
    [Trait("Spec", "SPEC-0009:UT-01")]
    public void GivenFixedInputsWhenSignatureIsComputedThenItEqualsTheReferenceVector()
    {
        var signature = OperatorRequestSignature.Compute(KeyMaterial, Timestamp, Method, Path, Encoding.UTF8.GetBytes(Body));

        Assert.Equal(ReferenceVector, signature);
        Assert.True(OperatorRequestSignature.Matches(KeyMaterial, Timestamp, Method, Path, Encoding.UTF8.GetBytes(Body), ReferenceVector));
    }

    [Fact(DisplayName = "Dado o vetor de referência, quando 1 byte do corpo é alterado, então a assinatura difere")]
    [Trait("Spec", "SPEC-0009:UT-01")]
    public void GivenOneBodyByteChangedWhenSignatureIsComputedThenItDiffers()
    {
        var body = Encoding.UTF8.GetBytes(Body);
        body[2] ^= 1;

        var signature = OperatorRequestSignature.Compute(KeyMaterial, Timestamp, Method, Path, body);

        Assert.Equal(ReferenceVectorWithOneByteChanged, signature);
        Assert.NotEqual(ReferenceVector, signature, StringComparer.Ordinal);
        Assert.False(OperatorRequestSignature.Matches(KeyMaterial, Timestamp, Method, Path, body, ReferenceVector));
    }

    [Fact(DisplayName = "Dado um corpo vazio, quando a assinatura é calculada, então o hash do corpo vazio entra na mensagem")]
    [Trait("Spec", "SPEC-0009:UT-01")]
    public void GivenEmptyBodyWhenSignatureIsComputedThenItEqualsTheReferenceVector()
    {
        Assert.Equal(ReferenceVectorEmptyBody, OperatorRequestSignature.Compute(KeyMaterial, Timestamp, "GET", Path, []));
    }

    [Theory(DisplayName = "Dado o vetor de referência, quando timestamp, método, caminho ou segredo mudam, então a assinatura não confere")]
    [Trait("Spec", "SPEC-0009:UT-01")]
    [InlineData(Timestamp + 1, Method, Path, "apogeu-ut01-reference-key")]
    [InlineData(Timestamp, "PUT", Path, "apogeu-ut01-reference-key")]
    [InlineData(Timestamp, Method, "/api/v1/operator/launch2", "apogeu-ut01-reference-key")]
    [InlineData(Timestamp, Method, Path, "apogeu-ut01-other-key")]
    public void GivenAnyCoveredPartChangedWhenMatchedThenSignatureIsRejected(long timestamp, string method, string path, string key)
    {
        Assert.False(OperatorRequestSignature.Matches(Encoding.UTF8.GetBytes(key), timestamp, method, path, Encoding.UTF8.GetBytes(Body), ReferenceVector));
    }
}
