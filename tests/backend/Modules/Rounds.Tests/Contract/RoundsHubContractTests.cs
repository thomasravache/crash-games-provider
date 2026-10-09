using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Rounds.Tests.Contract;

[Collection(RoundsIntegration.Name)]
public sealed class RoundsHubContractTests(RoundsContainers containers)
{
    [Fact(DisplayName = "Dado o contrato do hub gerado (SPEC-0006), quando as mensagens reais de IT-03 são validadas contra o esquema gerado, então todas conferem")]
    [Trait("Spec", "SPEC-0008:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedHubContractWhenRealMessagesAreValidatedThenAllConform()
    {
        var ct = TestContext.Current.CancellationToken;
        var manifest = await RoundsHubManifest.GenerateAsync(ct);
        await using var cluster = await RoundsCluster.CreateAsync(containers, sala: null, ct);
        await using var captured = await HubScenario.CaptureRoundWithSyncAsync(cluster, ct);
        var violations = new List<string>();

        foreach (var message in captured.Recorder.Events)
        {
            violations.AddRange(HubSchemaValidator.Validate(manifest.ClientArgument(message.Target), message.Payload)
                .Select(error => $"{message.Target}: {error} in {message.Payload.GetRawText()}"));
        }

        violations.AddRange(HubSchemaValidator.Validate(manifest.ServerResult("JoinSala"), captured.Snapshot)
            .Select(error => $"JoinSala: {error} in {captured.Snapshot.GetRawText()}"));
        violations.AddRange(HubSchemaValidator.Validate(manifest.ServerResult("GetServerTime"), captured.ServerTime)
            .Select(error => $"GetServerTime: {error} in {captured.ServerTime.GetRawText()}"));

        var targets = captured.Recorder.Events.Select(e => e.Target).ToHashSet(StringComparer.Ordinal);
        Assert.True(
            targets.IsSupersetOf(["RoundBetting", "RoundFlying", "RoundSync", "RoundCrashed"]),
            $"Expected real Betting, Flying, Sync and Crashed messages, got [{string.Join(", ", targets)}].");
        Assert.True(violations.Count == 0, "Messages that do not conform to the generated hub contract:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }
}
