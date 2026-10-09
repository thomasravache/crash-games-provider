using System.Text;
using Apogeu.BuildingBlocks.Security;
using Apogeu.TestKit.Time;
using Xunit;

namespace Apogeu.Security.Tests.Unit;

/// <summary>UT-02: the ±300 s window of the operator signature, on a frozen clock (FakeTimeProvider).</summary>
public sealed class OperatorRequestVerifierTests
{
    private const string Path = "/api/v1/operator/launch";
    private static readonly byte[] KeyMaterial = Encoding.UTF8.GetBytes("apogeu-ut02-test-key");
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"playerId":"player-ut02"}""");

    [Theory(DisplayName = "Dado o relógio fixo, quando o timestamp está a 299 s (antes ou depois), então a requisição é aceita")]
    [Trait("Spec", "SPEC-0009:UT-02")]
    [InlineData(-299)]
    [InlineData(299)]
    [InlineData(0)]
    public void GivenFixedClockWhenTimestampIsWithin299SecondsThenRequestIsAccepted(int offsetSeconds)
    {
        var clock = TestClock.Create();
        var verifier = new OperatorRequestVerifier(clock);

        var verdict = verifier.Verify(SignedAt(clock.GetUtcNow().ToUnixTimeSeconds() + offsetSeconds), KeyMaterial);

        Assert.Equal(OperatorSignatureVerdict.Valid, verdict);
        Assert.Null(OperatorRequestVerifier.ErrorCodeOf(verdict));
    }

    [Theory(DisplayName = "Dado o relógio fixo, quando o timestamp está a 301 s (antes ou depois), então TIMESTAMP_OUT_OF_WINDOW")]
    [Trait("Spec", "SPEC-0009:UT-02")]
    [InlineData(-301)]
    [InlineData(301)]
    public void GivenFixedClockWhenTimestampIsAt301SecondsThenTimestampOutOfWindow(int offsetSeconds)
    {
        var clock = TestClock.Create();
        var verifier = new OperatorRequestVerifier(clock);

        // Correctly signed: only the window can reject it.
        var verdict = verifier.Verify(SignedAt(clock.GetUtcNow().ToUnixTimeSeconds() + offsetSeconds), KeyMaterial);

        Assert.Equal(OperatorSignatureVerdict.TimestampOutOfWindow, verdict);
        Assert.Equal("TIMESTAMP_OUT_OF_WINDOW", OperatorRequestVerifier.ErrorCodeOf(verdict));
    }

    [Fact(DisplayName = "Dado um timestamp aceito, quando o relógio avança 2 s além da janela, então a mesma requisição passa a ser rejeitada")]
    [Trait("Spec", "SPEC-0009:UT-02")]
    public void GivenAcceptedRequestWhenClockMovesPastWindowThenItIsRejected()
    {
        var clock = TestClock.Create();
        var verifier = new OperatorRequestVerifier(clock);
        var request = SignedAt(clock.GetUtcNow().ToUnixTimeSeconds() - 299);

        Assert.Equal(OperatorSignatureVerdict.Valid, verifier.Verify(request, KeyMaterial));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(OperatorSignatureVerdict.TimestampOutOfWindow, verifier.Verify(request, KeyMaterial));
    }

    [Fact(DisplayName = "Dado um timestamp na janela, quando a assinatura é de outro segredo, então INVALID_SIGNATURE")]
    [Trait("Spec", "SPEC-0009:UT-02")]
    public void GivenTimestampInsideWindowWhenSignedWithAnotherSecretThenInvalidSignature()
    {
        var clock = TestClock.Create();
        var verifier = new OperatorRequestVerifier(clock);

        var verdict = verifier.Verify(SignedAt(clock.GetUtcNow().ToUnixTimeSeconds()), Encoding.UTF8.GetBytes("apogeu-ut02-other-key"));

        Assert.Equal(OperatorSignatureVerdict.InvalidSignature, verdict);
        Assert.Equal("INVALID_SIGNATURE", OperatorRequestVerifier.ErrorCodeOf(verdict));
    }

    private static OperatorSignedRequest SignedAt(long timestamp) =>
        new(timestamp, "POST", Path, Body, OperatorRequestSignature.Compute(KeyMaterial, timestamp, "POST", Path, Body));
}
