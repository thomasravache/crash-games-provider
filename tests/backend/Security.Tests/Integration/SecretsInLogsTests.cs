using System.Net;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.TestKit.Logging;
using Xunit;

namespace Apogeu.Security.Tests.Integration;

/// <summary>
/// IT-07: secrets, signatures, launch tokens and session tokens never reach the logs (SPEC-0009 §5, ADR-0014). The capture
/// sees every entry before any formatter or exporter, at Trace level for the Apogeu categories, so masking only in the
/// console output would not be enough: the values must not be written at all.
/// </summary>
[Collection(SecurityDependencies.Name)]
public sealed class SecretsInLogsTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dada a captura de logs, quando lançamento e sessão ocorrem (aceitos e rejeitados), então segredo, assinatura, launchToken e sessionToken não aparecem")]
    [Trait("Spec", "SPEC-0009:IT-07")]
    [Trait("Category", "Integration")]
    public async Task GivenLogCaptureWhenLaunchAndSessionHappenThenNoSecretAppearsInLogs()
    {
        var ct = TestContext.Current.CancellationToken;
        var capture = new LogCapture();
        await using var host = await SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions { LogCapture = capture }, ct);
        using var client = host.CreateClient();
        var sensitive = new List<(string Name, string Value)>
        {
            ("primary key secret", host.PrimaryKey.Secret),
            ("secondary key secret", host.SecondaryKey.Secret),
            ("session signing key", host.SessionSigningKey),
        };

        // Accepted launch, then its signature replayed (rejected), then a launch with a wrong signature (rejected).
        var timestamp = host.UnixNow();
        var body = LaunchBodies.Demo(LaunchBodies.NewPlayerId());
        var signature = ReferenceSignature.Compute(host.PrimaryKey.SecretBytes, timestamp, "POST", OperatorApi.LaunchPath, body);
        sensitive.Add(("accepted request signature", signature));
        string launchToken;
        using (var request = OperatorApi.Launch(body, host.PrimaryKey.Id, timestamp, signature))
        using (var response = await client.SendAsync(request, ct))
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {text}");
            using var json = System.Text.Json.JsonDocument.Parse(text);
            launchToken = LaunchUrls.TokenOf(new Uri(json.RootElement.GetProperty("launchUrl").GetString()!));
        }

        sensitive.Add(("launch token", launchToken));
        using (var replay = OperatorApi.Launch(body, host.PrimaryKey.Id, timestamp, signature))
        using (await client.SendAsync(replay, ct))
        {
        }

        var wrong = ReferenceSignature.Compute(TestApiKey.Generate().SecretBytes, host.UnixNow(), "POST", OperatorApi.LaunchPath, body);
        sensitive.Add(("rejected request signature", wrong));
        using (var rejected = OperatorApi.Launch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.PrimaryKey.Id, host.UnixNow(), wrong))
        using (await client.SendAsync(rejected, ct))
        {
        }

        // Session exchanged, then the launch token reused (rejected).
        var sessionToken = await OperatorApi.CreateSessionTokenAsync(client, launchToken, ct);
        sensitive.Add(("session token", sessionToken));
        sensitive.Add(("session token signature", sessionToken[(sessionToken.LastIndexOf('.') + 1)..]));
        using (var reuse = OperatorApi.CreateSession(launchToken))
        using (await client.SendAsync(reuse, ct))
        {
        }

        AssertNoSensitiveValue(capture.Entries, sensitive);
    }

    private static void AssertNoSensitiveValue(IReadOnlyList<CapturedLogEntry> entries, List<(string Name, string Value)> sensitive)
    {
        // Not vacuous: the security flow itself is logged (accepted and rejected launches, observability of SPEC-0009 §8)
        // by the Operators module or the security building block, and the capture saw it.
        Assert.Contains(
            entries,
            e => e.Category.StartsWith("Apogeu.Operators.", StringComparison.Ordinal)
                || e.Category.StartsWith("Apogeu.BuildingBlocks.Security", StringComparison.Ordinal));

        foreach (var entry in entries)
        {
            var text = entry.Message + Environment.NewLine + entry.Exception;
            foreach (var (name, value) in sensitive)
            {
                Assert.False(
                    text.Contains(value, StringComparison.Ordinal),
                    $"The {name} appears in a log entry of '{entry.Category}' ({entry.Level}, event {entry.EventId}).");
            }
        }
    }
}
