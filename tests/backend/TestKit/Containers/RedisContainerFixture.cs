using Xunit;

#pragma warning disable MA0025 // Scaffold (SPEC-0003 phase 1): every stub is replaced by the Testcontainers code in the Green phase.

namespace Apogeu.TestKit.Containers;

/// <summary>
/// Real Redis in a container (Testcontainers, ADR-0009). Use it as a class or collection fixture so the container is
/// shared by the tests of the collection; it starts in <see cref="InitializeAsync"/> and is removed in <see cref="DisposeAsync"/>.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    /// <summary>The configuration string for StackExchange.Redis of the running container.</summary>
    public string ConnectionString => throw new NotImplementedException();

    public ValueTask InitializeAsync() => throw new NotImplementedException();

    public ValueTask DisposeAsync() => throw new NotImplementedException();
}
