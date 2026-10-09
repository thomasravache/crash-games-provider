using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;

namespace Apogeu.Security.Tests.Support;

/// <summary>
/// A minimal SignalR client over the JSON hub protocol on the TestServer's WebSocket (no SignalR client package):
/// handshake, then one invocation and its completion. Enough to prove what the server answers to <c>JoinSala</c>.
/// </summary>
internal sealed class HubProbe : IAsyncDisposable
{
    private const char RecordSeparator = '\u001e';
    private readonly WebSocket _socket;
    private readonly StringBuilder _pending = new();
    private int _nextInvocationId;

    private HubProbe(WebSocket socket) => _socket = socket;

    /// <summary>Connects to <paramref name="hubPath"/> with <c>access_token</c> in the query string, as browsers do.</summary>
    public static async Task<HubProbe> ConnectAsync(TestServer server, string hubPath, string accessToken, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(server);
        var client = server.CreateWebSocketClient();
        var uri = new Uri(SecurityHost.BaseAddress, $"{hubPath}?access_token={Uri.EscapeDataString(accessToken)}");
        var socket = await client.ConnectAsync(new UriBuilder(uri) { Scheme = "wss" }.Uri, ct).ConfigureAwait(false);
        var probe = new HubProbe(socket);
        await probe.SendAsync("""{"protocol":"json","version":1}""", ct).ConfigureAwait(false);
        var handshake = await probe.ReceiveAsync(ct).ConfigureAwait(false);
        using var json = JsonDocument.Parse(handshake);
        if (json.RootElement.TryGetProperty("error", out var error))
        {
            throw new Xunit.Sdk.XunitException($"SignalR handshake failed: {error.GetString()}");
        }

        return probe;
    }

    /// <summary>Invokes <paramref name="target"/> and returns the completion: its <c>result</c> or its <c>error</c>.</summary>
    public async Task<HubCompletion> InvokeAsync(string target, string argument, CancellationToken ct)
    {
        var id = (++_nextInvocationId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var invocation = JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["type"] = 1,
            ["invocationId"] = id,
            ["target"] = target,
            ["arguments"] = new[] { argument },
        });
        await SendAsync(invocation, ct).ConfigureAwait(false);

        while (true)
        {
            var message = await ReceiveAsync(ct).ConfigureAwait(false);
            using var json = JsonDocument.Parse(message);
            var root = json.RootElement;
            var type = root.GetProperty("type").GetInt32();
            if (type == 7)
            {
                var reason = root.TryGetProperty("error", out var closeError) ? closeError.GetString() : null;
                return new HubCompletion(null, reason ?? "connection closed by the server", root.GetRawText());
            }

            if (type == 3 && root.TryGetProperty("invocationId", out var invocationId) && string.Equals(invocationId.GetString(), id, StringComparison.Ordinal))
            {
                return new HubCompletion(
                    root.TryGetProperty("result", out var result) ? result.GetRawText() : null,
                    root.TryGetProperty("error", out var error) ? error.GetString() : null,
                    root.GetRawText());
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State == WebSocketState.Open)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
        }

        _socket.Dispose();
    }

    private Task SendAsync(string json, CancellationToken ct) =>
        _socket.SendAsync(Encoding.UTF8.GetBytes(json + RecordSeparator), WebSocketMessageType.Text, endOfMessage: true, ct);

    private async Task<string> ReceiveAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (true)
        {
            var text = _pending.ToString();
            var end = text.IndexOf(RecordSeparator, StringComparison.Ordinal);
            if (end >= 0)
            {
                _pending.Remove(0, end + 1);
                return text[..end];
            }

            var received = await _socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            if (received.MessageType == WebSocketMessageType.Close)
            {
                return """{"type":7,"error":"socket closed"}""";
            }

            _pending.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
        }
    }
}
