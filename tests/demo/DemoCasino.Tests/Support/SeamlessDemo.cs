using Apogeu.Security.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>
/// The demo casino and the real Apogeu wired as in Development with the demo operator in seamless mode (SPEC-0047): the
/// DemoCasino listens on the loopback (real HTTP) and is the demo operator's wallet (<c>walletUrl</c>); the Apogeu (Host in
/// memory on real PostgreSQL and Redis) calls it signed with the demo operator key, the same key the casino signs its own
/// calls to the Apogeu with. The casino reaches the Apogeu through the in-memory network of the test server.
/// </summary>
internal sealed class SeamlessDemo : IAsyncDisposable
{
    private SeamlessDemo(DemoCasinoHost casino, ApogeuApiHost apogeu, WalletCallRecorder walletCalls, TestApiKey key)
    {
        Casino = casino;
        Apogeu = apogeu;
        WalletCalls = walletCalls;
        Key = key;
    }

    public DemoCasinoHost Casino { get; }

    public ApogeuApiHost Apogeu { get; }

    /// <summary>The wallet API requests that reached the casino.</summary>
    public WalletCallRecorder WalletCalls { get; }

    /// <summary>The demo operator key (both directions).</summary>
    public TestApiKey Key { get; }

    public static async Task<SeamlessDemo> StartAsync(string postgresServerConnectionString, string redisConnectionString, CancellationToken cancellationToken)
    {
        var key = TestApiKey.Generate();
        var recorder = new WalletCallRecorder();
        ApogeuApiHost? apogeu = null;

        // The casino first (the Apogeu needs its address); its "Apogeu" client resolves the Apogeu's network lazily.
        var casino = DemoCasinoHost.StartListening(
            services =>
            {
                recorder.AddTo(services);
                services.AddHttpClient(LobbyCasino.ApogeuClientName).ConfigurePrimaryHttpMessageHandler(() =>
                    (apogeu ?? throw new InvalidOperationException("The Apogeu of the test is not started yet.")).CreateHandler());
            },
            settings: null,
            key.Id,
            key.Secret);
        try
        {
            apogeu = await ApogeuApiHost.StartAsync(postgresServerConnectionString, redisConnectionString, key, casino.ListeningUrl, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await casino.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return new SeamlessDemo(casino, apogeu, recorder, key);
    }

    public async ValueTask DisposeAsync()
    {
        await Apogeu.DisposeAsync().ConfigureAwait(false);
        await Casino.DisposeAsync().ConfigureAwait(false);
    }
}
