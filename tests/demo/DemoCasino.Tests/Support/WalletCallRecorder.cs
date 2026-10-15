using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>
/// Records, at the door of the DemoCasino (a middleware placed before the application's own pipeline by an
/// <see cref="IStartupFilter"/>), every request to its wallet API (<c>/apogeu/v1/*</c>) as it arrived: path, signature
/// headers and the exact body bytes, plus the status the casino answered (SPEC-0047 IT-01: "as chamadas chegam assinadas").
/// </summary>
internal sealed class WalletCallRecorder
{
    private const string WalletPrefix = "/apogeu/v1/";

    private readonly Lock _gate = new();
    private readonly List<ArrivedWalletCall> _calls = [];

    public IReadOnlyList<ArrivedWalletCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>Registers the recorder in the casino's services (pass to <see cref="DemoCasinoHost"/> configureServices).</summary>
    public void AddTo(IServiceCollection services)
    {
        services.AddSingleton(this);
        services.AddSingleton<IStartupFilter>(new Filter(this));
    }

    private async Task RecordAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments(WalletPrefix.TrimEnd('/'), StringComparison.Ordinal))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Request.EnableBuffering();
        using var buffer = new MemoryStream();
        await context.Request.Body.CopyToAsync(buffer, context.RequestAborted).ConfigureAwait(false);
        context.Request.Body.Position = 0;
        var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        await next(context).ConfigureAwait(false);

        lock (_gate)
        {
            _calls.Add(new ArrivedWalletCall(context.Request.Path.Value ?? string.Empty, headers, buffer.ToArray(), context.Response.StatusCode));
        }
    }

    private sealed class Filter(WalletCallRecorder recorder) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextDelegate) => recorder.RecordAsync(context, nextDelegate));
            next(app);
        };
    }
}
