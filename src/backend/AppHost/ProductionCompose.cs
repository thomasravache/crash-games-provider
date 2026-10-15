using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;

namespace Apogeu.AppHost;

/// <summary>Names and limits of the production compose the AppHost publishes (SPEC-0065 §6, ADR-0029).</summary>
internal static class ProductionCompose
{
    /// <summary>Port the api and the DemoCasino listen on inside the network, reached by name by the proxy (SPEC-0066).</summary>
    public const int ServicePort = 8080;

    public const string PostgresVolume = "postgres-data";

    /// <summary>Volume of the Data Protection key ring of the api (ADR-0014).</summary>
    public const string KeysVolume = "dataprotection-keys";

    /// <summary>Directory of the Data Protection keys in the api (<c>DataProtection:KeysPath</c>).</summary>
    public const string KeysPath = "/keys";

    /// <summary>Variable of the .env with the public address of the API (proxy and static build of the game).</summary>
    public const string PublicApiUrlVariable = "PUBLIC_API_URL";

    /// <summary>The named volume of the keys, declared at the top level of the compose file.</summary>
    public static Volume KeysVolumeDeclaration() => new()
    {
        Name = KeysVolume,
        Driver = "local",
    };

    /// <summary>The named volume of the keys mounted at <see cref="KeysPath"/> in the api.</summary>
    public static Volume KeysVolumeMount() => new()
    {
        Name = KeysVolume,
        Source = KeysVolume,
        Target = KeysPath,
        Type = "volume",
    };

    /// <summary>
    /// Memory limit of a service, so the production does not starve the shared VPS (ADR-0029), and its restart policy: the
    /// compose only orders the start (no health wait), so a service that starts before PostgreSQL answers (the api migrates
    /// on its start) or fails later comes back by itself.
    /// </summary>
    public static void LimitMemory(Service service, string memory)
    {
        service.Deploy = new() { Resources = new() { Limits = new() { Memory = memory } } };
        service.Restart = "unless-stopped";
    }
}
