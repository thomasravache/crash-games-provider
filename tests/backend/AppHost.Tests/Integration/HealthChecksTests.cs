using System.Net;
using System.Text.Json;
using Apogeu.AppHost.Tests.Support;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Xunit;

namespace Apogeu.AppHost.Tests.Integration;

public sealed class HealthChecksTests
{
    private static readonly TimeSpan HealthTransitionTimeout = TimeSpan.FromMinutes(2);

    [Fact(DisplayName = "Dado o ambiente em execução, quando o recurso postgres é parado, então /health retorna 503 citando postgres e /alive retorna 200")]
    [Trait("Spec", "SPEC-0005:IT-02")]
    public async Task GivenRunningEnvironmentWhenPostgresStopsThenHealthIsUnhealthyCitingPostgresAndAliveIsOk()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await AppHostEnvironment.BuildAsync(cancellationToken);
        AppHostEnvironment.AssertDeclaresContractResources(app);
        await app.StartAsync(cancellationToken).WaitAsync(AppHostEnvironment.StartupTimeout, cancellationToken);
        await AppHostEnvironment.WaitHealthyAsync(app, "postgres", cancellationToken);
        await AppHostEnvironment.WaitHealthyAsync(app, "api", cancellationToken);

        using var api = app.CreateHttpClient("api");

        // Control: with every dependency up, /health reports healthy.
        var healthy = await WaitForHealthStatusAsync(api, HttpStatusCode.OK, cancellationToken);
        Assert.True(healthy.Status == HttpStatusCode.OK, $"/health never became 200 with postgres running: {healthy.Describe()}");

        var stop = await app.ResourceCommands.ExecuteCommandAsync("postgres", KnownResourceCommands.StopCommand, cancellationToken);
        Assert.True(stop.Success, $"Setup failed: could not stop postgres ({stop.Message}).");

        var unhealthy = await WaitForHealthStatusAsync(api, HttpStatusCode.ServiceUnavailable, cancellationToken);
        Assert.True(
            unhealthy.Status == HttpStatusCode.ServiceUnavailable,
            $"/health did not become 503 after postgres stopped: {unhealthy.Describe()}");

        using var body = JsonDocument.Parse(unhealthy.Body);
        Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
        var entries = body.RootElement.GetProperty("entries");
        Assert.True(
            entries.TryGetProperty("postgres", out _),
            $"/health must cite the postgres entry. Body: {unhealthy.Body}");

        using var alive = await api.GetAsync(new Uri("/alive", UriKind.Relative), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
    }

    private static async Task<HealthResponse> WaitForHealthStatusAsync(
        HttpClient api,
        HttpStatusCode expected,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + HealthTransitionTimeout;
        HealthResponse last;
        do
        {
            using var response = await api.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken).ConfigureAwait(false);
            last = new HealthResponse(
                response.StatusCode,
                await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (last.Status == expected)
            {
                return last;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
        while (DateTime.UtcNow < deadline);

        return last;
    }

    private sealed record HealthResponse(HttpStatusCode Status, string Body)
    {
        public string Describe() => $"{(int)Status} {Body}";
    }
}
