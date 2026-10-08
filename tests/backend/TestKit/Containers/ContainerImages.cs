namespace Apogeu.TestKit.Containers;

/// <summary>
/// Pinned images of the real dependencies (ADR-0009, ADR-0010, ADR-0015): PostgreSQL 18, Redis 8 and Keycloak 26.
/// Changing a tag here changes it for every integration test.
/// </summary>
internal static class ContainerImages
{
    public const string PostgreSql = "postgres:18.3";
    public const string Redis = "redis:8.6";
    public const string Keycloak = "quay.io/keycloak/keycloak:26.6";
}
