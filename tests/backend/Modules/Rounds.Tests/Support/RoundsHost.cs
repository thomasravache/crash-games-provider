using System.Diagnostics.Metrics;
using System.Globalization;
using Apogeu.Rounds.Features.Conductor;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// One instance of the real Host (WebApplicationFactory) in Development, on the test's PostgreSQL database and the shared
/// Redis, with the test authentication scheme and a Data Protection key ring shared by every instance of the test (the
/// Fairness seeds encrypted by one instance are read by the next leader). Disposing it is "killing" the instance.
/// </summary>
internal sealed class RoundsHost : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private bool _disposed;

    public RoundsHost(string name, string databaseConnectionString, string redisConnectionString, DirectoryInfo keys, SalaSettings sala)
    {
        Name = name;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.UseSetting("ConnectionStrings:apogeu", databaseConnectionString);
            host.UseSetting("ConnectionStrings:redis", redisConnectionString);
            var section = $"Rounds:Salas:{SalaSettings.SalaId}:";
            host.UseSetting(section + "Rtp", sala.Rtp);
            host.UseSetting(section + "Cap", sala.Cap);
            host.UseSetting(section + "GrowthK", sala.GrowthK);
            host.UseSetting(section + "BettingDuration", sala.BettingDuration);
            host.UseSetting(section + "CooldownDuration", sala.CooldownDuration);
            host.UseSetting(section + "ChainLength", sala.ChainLength.ToString(CultureInfo.InvariantCulture));
            host.ConfigureServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.AddDataProtection().SetApplicationName("Apogeu.Rounds.Tests").PersistKeysToFileSystem(keys);
            });
        });
    }

    public string Name { get; }

    public IServiceProvider Services => _factory.Services;

    public bool IsLeading => Services.GetRequiredService<IRoundsLeadership>().IsLeading(SalaSettings.SalaId);

    public IMeterFactory MeterFactory => Services.GetRequiredService<IMeterFactory>();

    /// <summary>Starts the host (and its hosted services: migrations, leader election, conductor).</summary>
    public void Start() => _ = _factory.Server;

    public HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>A real SignalR client (long polling over the in-memory server) authenticated as <paramref name="player"/>.</summary>
    public HubConnection CreateHubConnection(string player = "player-1")
    {
        var server = _factory.Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/rounds"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Headers["Authorization"] = $"{TestAuthHandler.SchemeName} {player}";
            })
            .Build();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _factory.DisposeAsync().ConfigureAwait(false);
    }
}
