using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>
/// The DemoCasino of the lobby tests (SPEC-0044). Test seams, fixed by the contract (SPEC-0043 CT-01, SPEC-0044 §6 Emenda
/// v1): the casino calls the Apogeu through the named HttpClient "Apogeu", whose primary handler (the network) the test
/// swaps, so the real signing step still runs; and the 60 s cache of the lobby reads the <see cref="TimeProvider"/> of DI,
/// here a <see cref="FakeTimeProvider"/> at the current time that the test advances.
/// </summary>
internal static class LobbyCasino
{
    public const string ApogeuClientName = "Apogeu";

    /// <summary>A DemoCasino talking to the real Apogeu in memory, signed with the Apogeu's demo operator key.</summary>
    public static DemoCasinoHost Against(ApogeuApiHost apogeu, FakeTimeProvider clock, IReadOnlyDictionary<string, string>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(apogeu);
        return DemoCasinoHost.Start(
            services => Configure(services, apogeu.CreateHandler, clock),
            settings,
            apogeu.Key.Id,
            apogeu.Key.Secret);
    }

    /// <summary>A DemoCasino whose Apogeu network is <paramref name="network"/> (a stand-in, SPEC-0044 "dublê HTTP").</summary>
    public static DemoCasinoHost Against(HttpMessageHandler network, IReadOnlyDictionary<string, string>? settings = null) =>
        DemoCasinoHost.Start(services => Configure(services, () => network, NewClock()), settings);

    public static FakeTimeProvider NewClock() =>
        new(DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

    private static void Configure(IServiceCollection services, Func<HttpMessageHandler> network, FakeTimeProvider clock)
    {
        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
        services.AddHttpClient(ApogeuClientName).ConfigurePrimaryHttpMessageHandler(network);
    }
}
