using System.Net;
using System.Text.Json;
using Apogeu.Rounds.Tests.Support;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Apogeu.Rounds.Tests.Integration;

[Collection(RoundsIntegration.Name)]
public sealed class NotFoundTests(RoundsContainers containers)
{
    [Fact(DisplayName = "Dados id de sala desconhecido e limit 0/101, quando chamados hub e REST, então SALA_NOT_FOUND/404 e 400 INVALID_LIMIT")]
    [Trait("Spec", "SPEC-0008:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenUnknownSalaAndInvalidLimitWhenHubAndRestAreCalledThenSalaNotFoundAndInvalidLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await RoundsCluster.CreateAsync(containers, sala: null, ct);
        var host = cluster.StartHost();
        using var http = host.CreateClient();

        await AssertProblemAsync(http, "/api/v1/salas/nao-existe/rounds", HttpStatusCode.NotFound, "SALA_NOT_FOUND", ct);
        await AssertProblemAsync(http, "/api/v1/salas/demo/rounds?limit=0", HttpStatusCode.BadRequest, "INVALID_LIMIT", ct);
        await AssertProblemAsync(http, "/api/v1/salas/demo/rounds?limit=101", HttpStatusCode.BadRequest, "INVALID_LIMIT", ct);

        // Controls: the limits 1 and 100 are valid.
        foreach (var limit in (int[])[1, 100])
        {
            var valid = await http.GetAsync(new Uri($"/api/v1/salas/demo/rounds?limit={limit}", UriKind.Relative), ct);
            Assert.True(valid.StatusCode == HttpStatusCode.OK, $"limit={limit} must be accepted, got {(int)valid.StatusCode}: {await valid.Content.ReadAsStringAsync(ct)}");
        }

        // A session for the unknown room itself, so the sala policy of SPEC-0009 lets the call reach the hub.
        await using var connection = host.CreateHubConnection(salaId: "nao-existe");
        await connection.StartAsync(ct);
        var error = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync<JsonElement>("JoinSala", "nao-existe", ct));
        Assert.Contains("SALA_NOT_FOUND", error.Message, StringComparison.Ordinal);
    }

    // From the G4 review of SPEC-0008: a non-numeric limit is the same contract error as an out-of-range one.
    [Fact(DisplayName = "Dado limit não numérico (limit=abc), quando GET /salas/{id}/rounds, então 400 ProblemDetails com code INVALID_LIMIT")]
    [Trait("Spec", "SPEC-0008:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenNonNumericLimitWhenHistoryIsRequestedThenInvalidLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await RoundsCluster.CreateAsync(containers, sala: null, ct);
        var host = cluster.StartHost();
        using var http = host.CreateClient();

        await AssertProblemAsync(http, "/api/v1/salas/demo/rounds?limit=abc", HttpStatusCode.BadRequest, "INVALID_LIMIT", ct);
    }

    private static async Task AssertProblemAsync(HttpClient http, string path, HttpStatusCode status, string code, CancellationToken ct)
    {
        var response = await http.GetAsync(new Uri(path, UriKind.Relative), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Assert.True(response.StatusCode == status, $"GET {path}: expected {(int)status} {code}, got {(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(body);
        Assert.True(
            problem.RootElement.TryGetProperty("code", out var actual) && string.Equals(actual.GetString(), code, StringComparison.Ordinal),
            $"GET {path}: expected the ProblemDetails extension code = {code}: {body}");
    }
}
