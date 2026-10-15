using System.Net;
using System.Text;

namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>
/// Primary handler standing in for the Apogeu network (SPEC-0044 "dublê HTTP"): answers every request with a fixed status
/// and JSON body, or fails as an unreachable server (<see cref="Unreachable"/>). Counts the requests it saw.
/// </summary>
internal sealed class StubApogeuHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string? _json;
    private int _requests;

    private StubApogeuHandler(HttpStatusCode status, string? json)
    {
        _status = status;
        _json = json;
    }

    public int Requests => Volatile.Read(ref _requests);

    /// <summary>An Apogeu that answers <paramref name="status"/> with <paramref name="json"/>.</summary>
    public static StubApogeuHandler Answering(HttpStatusCode status, string json) => new(status, json);

    /// <summary>An Apogeu that cannot be reached: every request fails with <see cref="HttpRequestException"/>.</summary>
    public static StubApogeuHandler Unreachable() => new(HttpStatusCode.ServiceUnavailable, json: null);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        if (_json is null)
        {
            throw new HttpRequestException("Connection refused (api.apogeu.test:443).");
        }

        return Task.FromResult(new HttpResponseMessage(_status)
        {
            RequestMessage = request,
            Content = new StringContent(_json, Encoding.UTF8, "application/json"),
        });
    }
}
