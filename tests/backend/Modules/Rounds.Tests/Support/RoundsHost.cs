using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net.Http.Headers;
using Apogeu.Rounds.Features.Conductor;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// One instance of the real Host (WebApplicationFactory) in Development, on the test's PostgreSQL database and the shared
/// Redis, with a player-session signing key of its own (the hub and the REST endpoints are authenticated with real
/// SPEC-0009 sessions, <see cref="PlayerSessions"/>) and a Data Protection key ring shared by every instance of the test
/// (the Fairness seeds encrypted by one instance are read by the next leader). Disposing it is "killing" the instance.
/// </summary>
internal sealed class RoundsHost : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _sessionSigningKey = PlayerSessions.NewSigningKey();
    private bool _disposed;

    public RoundsHost(string name, string databaseConnectionString, string redisConnectionString, DirectoryInfo keys, RoundsHostSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        Name = name;
        var salas = setup.Salas ?? Demo(new SalaSettings());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.UseSetting("ConnectionStrings:apogeu", databaseConnectionString);
            host.UseSetting("ConnectionStrings:redis", setup.LeaderRedis ?? redisConnectionString);
            host.UseSetting(PlayerSessions.SettingName, _sessionSigningKey);
            foreach (var (salaId, sala) in salas)
            {
                var section = $"Rounds:Salas:{salaId}:";
                host.UseSetting(section + "Rtp", sala.Rtp);
                host.UseSetting(section + "Cap", sala.Cap);
                host.UseSetting(section + "GrowthK", sala.GrowthK);
                host.UseSetting(section + "BettingDuration", sala.BettingDuration);
                host.UseSetting(section + "CooldownDuration", sala.CooldownDuration);
                host.UseSetting(section + "ChainLength", sala.ChainLength.ToString(CultureInfo.InvariantCulture));
            }

            host.ConfigureServices(services =>
            {
                services.AddDataProtection().SetApplicationName("Apogeu.Rounds.Tests").PersistKeysToFileSystem(keys);
                if (setup.LeaderRedis is not null)
                {
                    // Only the leader lock goes through the given Redis: the SignalR backplane stays on the shared one.
                    services.PostConfigure<RedisOptions>(options =>
                    {
                        options.Configuration.EndPoints.Clear();
                        foreach (var endpoint in ConfigurationOptions.Parse(redisConnectionString).EndPoints)
                        {
                            options.Configuration.EndPoints.Add(endpoint);
                        }
                    });
                }

                setup.Services?.Invoke(services);
            });
        });
    }

    public string Name { get; }

    public IServiceProvider Services => _factory.Services;

    public bool IsLeading => Services.GetRequiredService<IRoundsLeadership>().IsLeading(SalaSettings.SalaId);

    public IMeterFactory MeterFactory => Services.GetRequiredService<IMeterFactory>();

    /// <summary>Starts the host (and its hosted services: migrations, leader election, conductor).</summary>
    public void Start() => _ = _factory.Server;

    /// <summary>An HTTP client with the session of <paramref name="player"/> in <paramref name="salaId"/> as bearer.</summary>
    public HttpClient CreateClient(string player = "player-1", string salaId = SalaSettings.SalaId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PlayerSessions.Issue(_sessionSigningKey, player, salaId));
        return client;
    }

    /// <summary>
    /// A real SignalR client (long polling over the in-memory server) with the session of <paramref name="player"/> in
    /// <paramref name="salaId"/>: <c>JoinSala</c> of another room is refused by the sala policy of SPEC-0009.
    /// </summary>
    public HubConnection CreateHubConnection(string player = "player-1", string salaId = SalaSettings.SalaId)
    {
        var server = _factory.Server;
        var session = PlayerSessions.Issue(_sessionSigningKey, player, salaId);
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/rounds"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(session);
            })
            .Build();
    }

    private static Dictionary<string, SalaSettings> Demo(SalaSettings sala) =>
        new(StringComparer.Ordinal) { [SalaSettings.SalaId] = sala };

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
