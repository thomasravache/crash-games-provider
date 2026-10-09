using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// How one <see cref="RoundsHost"/> differs from the cluster default (G4 review of SPEC-0008): its own rooms
/// (<c>Rounds:Salas</c>), the Redis of its leader lock (e.g. a <see cref="FreezableTcpProxy"/>) and extra test services.
/// </summary>
internal sealed record RoundsHostSetup
{
    /// <summary>The configured rooms by id; <see langword="null"/> → only <c>demo</c> with the cluster settings.</summary>
    public IReadOnlyDictionary<string, SalaSettings>? Salas { get; init; }

    /// <summary><c>ConnectionStrings:redis</c> of the leader lock; the SignalR backplane stays on the shared Redis.</summary>
    public string? LeaderRedis { get; init; }

    /// <summary>Extra registrations (test doubles, decorators) applied after the Host's own.</summary>
    public Action<IServiceCollection>? Services { get; init; }
}
