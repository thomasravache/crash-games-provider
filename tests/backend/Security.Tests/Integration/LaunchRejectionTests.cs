using System.Net;
using System.Text;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Integration;

/// <summary>IT-02: every rejection of the launch endpoint, with its status and code (SPEC-0009 §6), on the real Host.</summary>
[Collection(SecurityDependencies.Name)]
public sealed class LaunchRejectionTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dada uma chave desconhecida, quando o lançamento é chamado, então 401 UNKNOWN_KEY")]
    [Trait("Spec", "SPEC-0009:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenUnknownKeyWhenLaunchingThenUnknownKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var stranger = TestApiKey.Generate();

        using var request = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), stranger, host.UnixNow());
        using var response = await client.SendAsync(request, ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "UNKNOWN_KEY", ct);
    }

    [Fact(DisplayName = "Dada uma assinatura errada (outro segredo ou corpo adulterado), quando o lançamento é chamado, então 401 INVALID_SIGNATURE")]
    [Trait("Spec", "SPEC-0009:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenWrongSignatureWhenLaunchingThenInvalidSignature()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();

        // Known key, signed with another secret.
        using (var request = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.PrimaryKey.Id, TestApiKey.Generate().SecretBytes, host.UnixNow()))
        using (var response = await client.SendAsync(request, ct))
        {
            await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "INVALID_SIGNATURE", ct);
        }

        // Correct signature of one body, sent with another body (the attacker changes the player).
        var timestamp = host.UnixNow();
        var signed = LaunchBodies.Demo("player-signed");
        var signature = ReferenceSignature.Compute(host.PrimaryKey.SecretBytes, timestamp, "POST", OperatorApi.LaunchPath, signed);
        using (var tampered = OperatorApi.Launch(LaunchBodies.Demo("player-tampered"), host.PrimaryKey.Id, timestamp, signature))
        using (var response = await client.SendAsync(tampered, ct))
        {
            await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "INVALID_SIGNATURE", ct);
        }

        // Signature that is not hex at all.
        using (var garbage = OperatorApi.Launch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.PrimaryKey.Id, host.UnixNow(), "not-a-signature"))
        using (var response = await client.SendAsync(garbage, ct))
        {
            await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "INVALID_SIGNATURE", ct);
        }
    }

    [Theory(DisplayName = "Dado um timestamp fora da janela (±301 s) e assinatura correta, quando o lançamento é chamado, então 401 TIMESTAMP_OUT_OF_WINDOW")]
    [Trait("Spec", "SPEC-0009:IT-02")]
    [Trait("Category", "Integration")]
    [InlineData(-301)]
    [InlineData(301)]
    public async Task GivenTimestampOutsideWindowWhenLaunchingThenTimestampOutOfWindow(int offsetSeconds)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();

        using var request = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.PrimaryKey, host.UnixNow() + offsetSeconds);
        using var response = await client.SendAsync(request, ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "TIMESTAMP_OUT_OF_WINDOW", ct);
    }

    [Fact(DisplayName = "Dado um corpo sem playerId corretamente assinado, quando o lançamento é chamado, então 400 VALIDATION_ERROR")]
    [Trait("Spec", "SPEC-0009:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenBodyWithoutPlayerIdWhenLaunchingThenValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();

        using var request = OperatorApi.SignedLaunch(LaunchBodies.WithoutPlayerId(), host.PrimaryKey, host.UnixNow());
        using var response = await client.SendAsync(request, ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR", ct);
    }

    [Fact(DisplayName = "Dado mode:\"real\" corretamente assinado, quando o lançamento é chamado, então 403 REAL_MONEY_DISABLED e nenhuma launchUrl")]
    [Trait("Spec", "SPEC-0009:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenRealMoneyModeWhenLaunchingThenRealMoneyDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();

        using var request = OperatorApi.SignedLaunch(LaunchBodies.RealMoney(LaunchBodies.NewPlayerId()), host.PrimaryKey, host.UnixNow());
        using var response = await client.SendAsync(request, ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.Forbidden, "REAL_MONEY_DISABLED", ct);
        Assert.DoesNotContain("launchUrl", await response.Content.ReadAsStringAsync(ct), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Dada uma requisição sem os cabeçalhos de assinatura, quando o lançamento é chamado, então 401 (negação por padrão)")]
    [Trait("Spec", "SPEC-0009:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenUnsignedRequestWhenLaunchingThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();

        using var content = new ByteArrayContent(LaunchBodies.Demo(LaunchBodies.NewPlayerId()));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        using var response = await client.PostAsync(new Uri(OperatorApi.LaunchPath, UriKind.Relative), content, ct);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 for an unsigned launch, got {(int)response.StatusCode}: {Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(ct))}");
    }

    private Task<SecurityHost> StartAsync(CancellationToken ct) =>
        SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions(), ct);
}
