using Testcontainers.Keycloak;
using Xunit;

namespace Apogeu.TestKit.Containers;

/// <summary>
/// Real Keycloak in a container (Testcontainers, ADR-0009). Use it as a class or collection fixture so the container is
/// shared by the tests of the collection; it starts in <see cref="InitializeAsync"/> and is removed in <see cref="DisposeAsync"/>.
/// </summary>
public sealed class KeycloakContainerFixture : IAsyncLifetime
{
    private readonly KeycloakContainer _container = new KeycloakBuilder(ContainerImages.Keycloak).Build();

    /// <summary>The base address of the Keycloak server of the running container.</summary>
    public string ConnectionString => _container.GetBaseAddress();

    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
