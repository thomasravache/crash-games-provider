using System.Net;
using System.Net.Sockets;
using StackExchange.Redis;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// A TCP proxy in front of the shared Redis whose traffic can be frozen: after <see cref="Freeze"/> no byte crosses it in
/// either direction, but no socket is closed or reset. A Host whose leader lock goes through it loses Redis the
/// ungraceful way (a hung connection, as in a network partition or a paused process): its renewals stop without the
/// release path running (G4 review of SPEC-0008, IT-01).
/// </summary>
internal sealed class FreezableTcpProxy : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly string _targetHost;
    private readonly int _targetPort;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private readonly List<TcpClient> _sockets = [];
    private readonly Task _accepting;
    private volatile bool _frozen;

    private FreezableTcpProxy(string targetHost, int targetPort)
    {
        _targetHost = targetHost;
        _targetPort = targetPort;
        _listener.Start();
        _accepting = AcceptAsync();
    }

    /// <summary>The Redis connection string of the proxy.</summary>
    public string ConnectionString => $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

    /// <summary>Starts a proxy to the Redis of <paramref name="redisConnectionString"/>.</summary>
    public static FreezableTcpProxy Start(string redisConnectionString)
    {
        var endpoint = ConfigurationOptions.Parse(redisConnectionString).EndPoints[0];
        return endpoint switch
        {
            DnsEndPoint dns => new FreezableTcpProxy(dns.Host, dns.Port),
            IPEndPoint ip => new FreezableTcpProxy(ip.Address.ToString(), ip.Port),
            _ => throw new InvalidOperationException($"Unsupported Redis endpoint {endpoint}."),
        };
    }

    /// <summary>Stops forwarding in both directions, keeping every socket open.</summary>
    public void Freeze() => _frozen = true;

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        lock (_gate)
        {
            foreach (var socket in _sockets)
            {
                socket.Dispose();
            }
        }

        try
        {
            await _accepting.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // The listener was stopped.
        }

        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            var upstream = new TcpClient();
            lock (_gate)
            {
                _sockets.Add(client);
                _sockets.Add(upstream);
            }

            _ = RelayAsync(client, upstream);
        }
    }

    private async Task RelayAsync(TcpClient client, TcpClient upstream)
    {
        try
        {
            await upstream.ConnectAsync(_targetHost, _targetPort, _stop.Token).ConfigureAwait(false);
            var downstream = client.GetStream();
            var toRedis = upstream.GetStream();
            await Task.WhenAny(PumpAsync(downstream, toRedis), PumpAsync(toRedis, downstream)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
            // The proxy stopped or a side closed.
        }
    }

    private async Task PumpAsync(NetworkStream from, NetworkStream to)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (true)
            {
                var read = await from.ReadAsync(buffer, _stop.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                // Frozen: the bytes are held (never delivered) and the socket stays open.
                while (_frozen)
                {
                    await Task.Delay(10, _stop.Token).ConfigureAwait(false);
                }

                await to.WriteAsync(buffer.AsMemory(0, read), _stop.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
            // The proxy stopped or a side closed.
        }
    }
}
