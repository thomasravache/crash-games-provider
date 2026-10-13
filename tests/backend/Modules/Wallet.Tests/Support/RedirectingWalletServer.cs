using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>
/// An operator wallet that answers every call with a redirect (<see cref="StatusCode"/>, 307 or 308, which keep the method
/// and the body) to the same path on <see cref="Target"/> (SPEC-0046 G4): the adapter must not follow it, so the target
/// must receive nothing. A real Kestrel server on the loopback, so the redirect reaches the real HTTP handler of the module.
/// </summary>
internal sealed class RedirectingWalletServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private int _hits;

    private RedirectingWalletServer(WebApplication app, Uri baseUrl, Uri target, int statusCode)
    {
        _app = app;
        BaseUrl = baseUrl;
        Target = target;
        StatusCode = statusCode;
    }

    /// <summary>Base URL of this wallet (the <c>walletUrl</c> configured for the operator).</summary>
    public Uri BaseUrl { get; }

    /// <summary>Where every call is redirected to.</summary>
    public Uri Target { get; }

    /// <summary>The redirect status answered.</summary>
    public int StatusCode { get; }

    /// <summary>How many calls this wallet answered with the redirect.</summary>
    public int Hits => Volatile.Read(ref _hits);

    public static async Task<RedirectingWalletServer> StartAsync(Uri target, int statusCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "RedirectingWalletServer" });
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        RedirectingWalletServer? server = null;
        app.MapPost("/apogeu/v1/{operation}", (HttpContext context) => server!.Redirect(context));

        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            server = new RedirectingWalletServer(app, new Uri(address), target, statusCode);
            return server;
        }
        catch
        {
            await app.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }

    private Task Redirect(HttpContext context)
    {
        Interlocked.Increment(ref _hits);
        var path = context.Request.PathBase.Add(context.Request.Path).Value ?? string.Empty;
        context.Response.StatusCode = StatusCode;
        context.Response.Headers.Location = new Uri(Target, path.TrimStart('/')).AbsoluteUri;
        return Task.CompletedTask;
    }
}
