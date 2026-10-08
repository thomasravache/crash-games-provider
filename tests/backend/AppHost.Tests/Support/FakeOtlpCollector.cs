using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>
/// Minimal OTLP/HTTP receiver on a loopback port. It keeps the raw protobuf payloads per signal so the tests can
/// look for byte sequences (trace ids, strings) in exactly what the real OTLP exporter sent.
/// </summary>
internal sealed class FakeOtlpCollector : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<byte[]>> _payloads = new(StringComparer.Ordinal);

    private FakeOtlpCollector(WebApplication app) => _app = app;

    public Uri Endpoint { get; private set; } = null!;

    public static async Task<FakeOtlpCollector> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var collector = new FakeOtlpCollector(app);

        app.MapPost("/v1/{signal}", async (string signal, HttpRequest request) =>
        {
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted).ConfigureAwait(false);
            collector._payloads.GetOrAdd(signal, _ => new ConcurrentQueue<byte[]>()).Enqueue(buffer.ToArray());

            // An empty body is a valid (empty) Export*ServiceResponse.
            return Results.Bytes([], "application/x-protobuf");
        });

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        collector.Endpoint = new Uri(address);
        return collector;
    }

    /// <summary>Raw payloads received for a signal: "traces", "logs" or "metrics".</summary>
    public IReadOnlyList<byte[]> Payloads(string signal) =>
        _payloads.TryGetValue(signal, out var queue) ? [.. queue] : [];

    public bool Contains(string signal, ReadOnlySpan<byte> sequence)
    {
        foreach (var payload in Payloads(signal))
        {
            if (payload.AsSpan().IndexOf(sequence) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Polls until <paramref name="condition"/> holds or the timeout elapses (exporters batch asynchronously).</summary>
    public static async Task<bool> EventuallyAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}
