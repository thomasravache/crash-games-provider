using Apogeu.ServiceDefaults.Tests.Support;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

namespace Apogeu.ServiceDefaults.Tests.Unit;

public sealed class SensitiveDataMaskingProcessorTests
{
    private const string Mask = "***";

    [Fact(DisplayName = "Dado um log com serverSeed, token e amount, quando passa pelo processador de mascaramento, então serverSeed e token viram *** e amount permanece")]
    [Trait("Spec", "SPEC-0005:UT-01")]
    public void GivenLogWithServerSeedTokenAndAmountWhenProcessedThenSensitiveValuesAreMaskedAndAmountIsKept()
    {
        var attributes = EmitThroughMaskingPipeline(
        [
            new("serverSeed", "4f1c9a7e-server-seed"),
            new("token", "session-token-value"),
            new("amount", 12.5m),
        ]);

        Assert.Equal(Mask, Value(attributes, "serverSeed"));
        Assert.Equal(Mask, Value(attributes, "token"));
        Assert.Equal(12.5m, Value(attributes, "amount"));
    }

    [Theory(DisplayName = "Dado um log com uma chave sensível, quando passa pelo processador de mascaramento, então o valor vira *** e as demais propriedades permanecem")]
    [Trait("Spec", "SPEC-0005:UT-01")]
    [InlineData("serverSeed")]
    [InlineData("token")]
    [InlineData("secret")]
    [InlineData("signature")]
    [InlineData("password")]
    [InlineData("authorization")]
    public void GivenLogWithSensitiveKeyWhenProcessedThenItsValueIsMasked(string sensitiveKey)
    {
        var attributes = EmitThroughMaskingPipeline(
        [
            new(sensitiveKey, "sensitive-value"),
            new("roundId", "round-42"),
        ]);

        Assert.Equal(Mask, Value(attributes, sensitiveKey));
        Assert.Equal("round-42", Value(attributes, "roundId"));
    }

    /// <summary>
    /// Emits one structured log through a real OpenTelemetry logging pipeline where the masking processor runs
    /// first and a capturing processor (standing in for the exporter) runs after it.
    /// </summary>
    private static IReadOnlyList<KeyValuePair<string, object?>> EmitThroughMaskingPipeline(
        List<KeyValuePair<string, object?>> state)
    {
        var captured = new CapturingLogProcessor();
        using (var loggerFactory = LoggerFactory.Create(logging => logging.AddOpenTelemetry(options =>
        {
            options.AddProcessor(new SensitiveDataMaskingProcessor());
            options.AddProcessor(captured);
        })))
        {
            var logger = loggerFactory.CreateLogger("SPEC-0005.UT-01");
            logger.Log(LogLevel.Information, default, state, exception: null, static (_, _) => "evento de teste");
        }

        var record = Assert.Single(captured.Records);
        return record;
    }

    private static object? Value(IReadOnlyList<KeyValuePair<string, object?>> attributes, string key) =>
        Assert.Single(attributes, attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal)).Value;
}
