using Apogeu.Rounds.Features.Live;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// Decorates the Host's real <see cref="IRoundBroadcaster"/> of one instance and records every message its conductor
/// sends, at the moment of the call, before the backplane: the attribution of hub messages to an instance that the
/// contract does not carry (G4 review of SPEC-0008, IT-01).
/// </summary>
internal sealed class AttributedBroadcaster(IRoundBroadcaster inner) : IRoundBroadcaster
{
    private readonly Lock _gate = new();
    private readonly List<AttributedBroadcast> _sent = [];

    public IReadOnlyList<AttributedBroadcast> Sent
    {
        get
        {
            lock (_gate)
            {
                return [.. _sent];
            }
        }
    }

    /// <summary>Replaces the registered <see cref="IRoundBroadcaster"/> with a recording decorator that is exposed through <paramref name="capture"/>.</summary>
    public static void Decorate(IServiceCollection services, Action<AttributedBroadcaster> capture)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(capture);
        var real = services.Last(d => d.ServiceType == typeof(IRoundBroadcaster));
        services.Remove(real);
        services.AddSingleton<IRoundBroadcaster>(provider =>
        {
            var decorated = new AttributedBroadcaster((IRoundBroadcaster)ActivatorUtilities.CreateInstance(provider, real.ImplementationType!));
            capture(decorated);
            return decorated;
        });
    }

    public IRoundsClient Sala(string salaId) => new Client(this, inner.Sala(salaId));

    private Task Record(string target, Guid roundId, Func<Task> send)
    {
        lock (_gate)
        {
            _sent.Add(new AttributedBroadcast(DateTimeOffset.UtcNow, target, roundId));
        }

        return send();
    }

    private sealed class Client(AttributedBroadcaster owner, IRoundsClient inner) : IRoundsClient
    {
        public Task RoundBetting(RoundBettingMessage message) =>
            owner.Record(nameof(RoundBetting), message.RoundId, () => inner.RoundBetting(message));

        public Task RoundFlying(RoundFlyingMessage message) =>
            owner.Record(nameof(RoundFlying), message.RoundId, () => inner.RoundFlying(message));

        public Task RoundSync(RoundSyncMessage message) =>
            owner.Record(nameof(RoundSync), message.RoundId, () => inner.RoundSync(message));

        public Task RoundCrashed(RoundCrashedMessage message) =>
            owner.Record(nameof(RoundCrashed), message.RoundId, () => inner.RoundCrashed(message));

        public Task RoundAnnulled(RoundAnnulledMessage message) =>
            owner.Record(nameof(RoundAnnulled), message.RoundId, () => inner.RoundAnnulled(message));
    }
}
