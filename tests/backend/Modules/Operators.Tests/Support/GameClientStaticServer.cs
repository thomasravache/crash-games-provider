using Apogeu.Repository.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Operators.Tests.Support;

/// <summary>
/// SPEC-0044 CT-01: the game client origin (<c>Operators:GameBaseUrl</c>) as the game front serves it — the files of
/// <c>src/frontend/apps/game-pipa/public</c> at the root, as Vite serves its public folder — on a real loopback port, so
/// the kit URLs of the catalog are fetched over HTTP like a browser would.
/// </summary>
internal sealed class GameClientStaticServer : IAsyncDisposable
{
    public const string PublicFolder = "src/frontend/apps/game-pipa/public";

    private readonly WebApplication _app;

    private GameClientStaticServer(WebApplication app, Uri baseUrl)
    {
        _app = app;
        BaseUrl = baseUrl;
    }

    public Uri BaseUrl { get; }

    public static async Task<GameClientStaticServer> StartAsync(CancellationToken cancellationToken)
    {
        var root = RepositoryRoot.Combine(PublicFolder);

        // A missing public folder serves nothing (404): that is the failure under test, not a setup error.
        IFileProvider files = Directory.Exists(root) ? new PhysicalFileProvider(root) : new NullFileProvider();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        var address = app.Urls.Single();
        return new GameClientStaticServer(app, new Uri(address));
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}
