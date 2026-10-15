using System.Net;

namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>
/// Primary handler that stands in for the Apogeu API: records each request as it left the casino's HTTP pipeline (after
/// the signing step) and answers 200 with an empty JSON object, without any network.
/// </summary>
internal sealed class CapturingHandler : HttpMessageHandler
{
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
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
