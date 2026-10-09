using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Apogeu.Fairness.Tests.Support;
using Xunit;

namespace Apogeu.Fairness.Tests.Contract;

public sealed class FairnessVectorsContractTests
{
    [Fact(DisplayName = "Dado tests/vectors/fairness-v1.json, quando validado contra o esquema dos vetores, então confere")]
    [Trait("Spec", "SPEC-0007:CT-01")]
    public void GivenVectorsFileWhenValidatedAgainstSchemaThenItConforms()
    {
        using var vectors = JsonDocument.Parse(File.ReadAllText(FairnessVectors.VectorsPath));
        using var schema = JsonDocument.Parse(File.ReadAllText(FairnessVectors.SchemaPath));

        var errors = JsonSchemaSubset.Validate(vectors.RootElement, schema.RootElement);

        Assert.True(errors.Count == 0, "fairness-v1.json violates its schema:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    [Fact(DisplayName = "Dado um vetor adulterado (hex maiúsculo, campo faltando), quando validado contra o esquema, então é rejeitado")]
    [Trait("Spec", "SPEC-0007:CT-01")]
    public void GivenTamperedVectorWhenValidatedAgainstSchemaThenItIsRejected()
    {
        var document = JsonNode.Parse(File.ReadAllText(FairnessVectors.VectorsPath))!;
        var first = document["vectors"]![0]!.AsObject();
        first["seed"] = first["seed"]!.GetValue<string>().ToUpperInvariant();
        first.Remove("crashPoint");
        using var tampered = JsonDocument.Parse(document.ToJsonString());
        using var schema = JsonDocument.Parse(File.ReadAllText(FairnessVectors.SchemaPath));

        var errors = JsonSchemaSubset.Validate(tampered.RootElement, schema.RootElement);

        Assert.Contains(errors, error => error.Contains(".seed", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("'crashPoint'", StringComparison.Ordinal));
    }

    [Theory(DisplayName = "Dado cada vetor de fairness-v1.json, quando recalculado pelo domínio, então semente anterior, ponto de crash e verificação conferem")]
    [MemberData(nameof(Vectors))]
    [Trait("Spec", "SPEC-0007:CT-01")]
    public void GivenEachVectorWhenRecomputedByDomainThenEverythingMatches(
        string description, string seed, string previousSeed, string salt, string rtp, string cap, string crashPoint)
    {
        Assert.NotNull(description);
        var seedValue = Seed.FromHex(seed);
        var saltValue = Salt.FromHex(salt);
        var rtpValue = new Rtp(Parse(rtp));
        var capValue = new Multiplier(Parse(cap));

        var next = SeedChain.Next(seedValue);
        var computed = CrashPointCalculator.Compute(seedValue, saltValue, rtpValue, capValue);
        var verification = FairnessVerifier.Verify(seedValue, Seed.FromHex(previousSeed), saltValue, rtpValue, capValue, new CrashPoint(Parse(crashPoint)));

        Assert.Equal(previousSeed, next.ToHex());
        Assert.Equal(crashPoint, computed.Value.ToString("0.00", CultureInfo.InvariantCulture));
        Assert.Equal(VerificationResult.Valid, verification);
    }

    [Fact(DisplayName = "Dado fairness-v1.json gerado pelo oráculo Python, quando recalculado pelo oráculo C# independente do domínio, então todos conferem")]
    [Trait("Spec", "SPEC-0007:CT-01")]
    public void GivenVectorsWhenRecomputedByIndependentOracleThenTheyMatch()
    {
        Assert.All(FairnessVectors.All(), vector =>
        {
            var seed = Convert.FromHexString(vector.Seed);
            Assert.Equal(vector.PreviousSeed, Convert.ToHexStringLower(ReferenceAlgorithm.NextLink(seed)));
            Assert.Equal(vector.CrashPointValue, ReferenceAlgorithm.CrashPoint(seed, Convert.FromHexString(vector.Salt), vector.RtpValue, vector.CapValue));
        });
    }

    public static TheoryData<string, string, string, string, string, string, string> Vectors => FairnessVectors.Rows();

    private static decimal Parse(string value) => decimal.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
}
