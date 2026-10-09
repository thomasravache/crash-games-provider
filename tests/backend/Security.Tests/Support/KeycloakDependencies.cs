using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Support;

/// <summary>Keycloak with the shipped realm, plus PostgreSQL and Redis for the Host (IT-06).</summary>
[CollectionDefinition(Name)]
public sealed class KeycloakDependencies
    : ICollectionFixture<KeycloakRealm>, ICollectionFixture<PostgreSqlContainerFixture>, ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "security-keycloak";
}
