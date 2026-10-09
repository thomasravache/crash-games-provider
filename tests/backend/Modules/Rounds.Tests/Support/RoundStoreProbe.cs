using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>Reads the rounds persisted in the test database through the real <see cref="IRoundStore"/>, with no Host running.</summary>
internal sealed class RoundStoreProbe : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public RoundStoreProbe(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddRoundsInfrastructure(_ => connectionString);
        _provider = services.BuildServiceProvider();
    }

    public async Task<Round?> FindAsync(Guid roundId, CancellationToken ct)
    {
        var scope = _provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await scope.ServiceProvider.GetRequiredService<IRoundStore>().FindAsync(new RoundId(roundId), ct).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
