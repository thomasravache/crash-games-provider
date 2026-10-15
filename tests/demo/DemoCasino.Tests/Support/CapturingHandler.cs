using System.Net;

namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>
/// Primary handler that stands in for the Apogeu API: records each request as it left the casino's HTTP pipeline (after
/// the signing step) and answers 200 with a fixed JSON body (an empty object by default), without any network.
/// </summary>
internal sealed class CapturingHandler(string responseJson = "{}") : HttpMessageHandler
{
    /// <summary>A 200 of <c>POST /api/v1/operator/launch</c> (SPEC-0009@1): a launch URL on the game origin of the tests.</summary>
    public const string LaunchResponse = """{"launchUrl":"https://game.apogeu.test/?lt=captured-launch-token","expiresAt":"2030-01-01T00:00:00.000Z"}""";

    private readonly List<CapturedRequest> _requests = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<CapturedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var headers = request.Headers
            .ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            _requests.Add(new CapturedRequest(request.Method, request.RequestUri!, headers, body));
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
