using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.Conductor;
using Apogeu.Rounds.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Two instances of the Host on the same PostgreSQL and Redis (SPEC-0023), every round crashing at a fixed point, each instance
/// with its observed <see cref="ICashOutArbiter"/> (<see cref="ArbiterProbe"/>) and its gated resume (<see cref="ResumeGate"/>).
/// </summary>
internal sealed class CashOutCluster : IAsyncDisposable
{
    private readonly (BettingHost Host, ArbiterProbe Probe, ResumeGate Gate) _first;
    private readonly (BettingHost Host, ArbiterProbe Probe, ResumeGate Gate) _second;
    private readonly bool _firstLed;

    private CashOutCluster((BettingHost, ArbiterProbe, ResumeGate) first, (BettingHost, ArbiterProbe, ResumeGate) second)
    {
        _first = first;
        _second = second;
        _firstLed = _first.Host.IsLeading;
    }

    /// <summary>The instance that led the room when the cluster started (it may be killed by the test).</summary>
    public BettingHost Leader => _firstLed ? _first.Host : _second.Host;

    /// <summary>The instance that did not lead the room when the cluster started.</summary>
    public BettingHost Follower => _firstLed ? _second.Host : _first.Host;

    public ArbiterProbe LeaderProbe => _firstLed ? _first.Probe : _second.Probe;

    public ArbiterProbe FollowerProbe => _firstLed ? _second.Probe : _first.Probe;

    public ResumeGate FollowerGate => _firstLed ? _second.Gate : _first.Gate;

    public ProgrammableWallet Wallet => (ProgrammableWallet)_first.Host.Wallet;

    public static async Task<CashOutCluster> StartAsync(BettingContainers containers, decimal crashPoint, BettingSala sala, CancellationToken ct)
    {
        var first = (Probe: new ArbiterProbe(), Gate: new ResumeGate());
        var second = (Probe: new ArbiterProbe(), Gate: new ResumeGate());
        var firstHost = await BettingHost.StartAsync(
            containers,
            ct,
            new ProgrammableWallet(),
            services => Configure(services, crashPoint, first.Probe, first.Gate),
            sala).ConfigureAwait(false);
        try
        {
            var secondHost = await BettingHost.StartPeerAsync(firstHost, services => Configure(services, crashPoint, second.Probe, second.Gate)).ConfigureAwait(false);
            await Eventually.HoldsAsync(() => firstHost.IsLeading || secondHost.IsLeading, TimeSpan.FromSeconds(30), () => "one instance leading the room").ConfigureAwait(false);
            return new CashOutCluster((firstHost, first.Probe, first.Gate), (secondHost, second.Probe, second.Gate));
        }
        catch
        {
            await firstHost.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _first.Probe.ReleaseLeader();
        _second.Probe.ReleaseLeader();
        await _second.Host.DisposeAsync().ConfigureAwait(false);
        await _first.Host.DisposeAsync().ConfigureAwait(false);
    }

    private static void Configure(IServiceCollection services, decimal crashPoint, ArbiterProbe probe, ResumeGate gate)
    {
        FixedCrashFairness.Register(services, crashPoint);
        services.Decorate<ICashOutArbiter>((inner, provider) =>
            new ObservedCashOutArbiter(inner, provider.GetRequiredService<IRoundsLeadership>(), probe));
        services.Decorate<IRoundStore>((inner, _) => new GatedRoundStore(inner, gate));
    }
}
