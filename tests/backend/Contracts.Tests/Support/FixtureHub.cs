using Apogeu.BuildingBlocks.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>
/// Strongly typed hub exposed only to the hub-contract generator in the tests. Only its signatures matter: a scalar
/// parameter with a nullable scalar result, a server-to-client stream, a nullable string parameter, lifecycle overrides
/// that must stay out of the contract, and a <see cref="CancellationToken"/> that is not part of the wire signature.
/// </summary>
public sealed class FixtureHub : Hub<IFixtureClient>
{
    private const string SignatureOnly = "Fixture hub: only its signatures are exported.";

    public Task<Multiplier?> FixtureCashout(Money stake) =>
        throw new NotSupportedException($"{SignatureOnly} ({nameof(FixtureCashout)}: {stake})");

    public IAsyncEnumerable<Multiplier> FixtureTicks(Money stake, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{SignatureOnly} ({nameof(FixtureTicks)}: {stake}, {cancellationToken.IsCancellationRequested})");

    public Task FixtureRename(string? nickname) =>
        throw new NotSupportedException($"{SignatureOnly} ({nameof(FixtureRename)}: {nickname})");

    public override Task OnConnectedAsync() => base.OnConnectedAsync();

    public override Task OnDisconnectedAsync(Exception? exception) => base.OnDisconnectedAsync(exception);
}
