using System.Reflection;

namespace Apogeu.Host.OpenApi;

/// <summary>
/// Build-time generation of the OpenAPI document (Microsoft.Extensions.ApiDescription.Server): the tool starts the Host
/// inside <c>GetDocument.Insider</c> with no deployment configuration, only to read the document. The required
/// connection strings get placeholders pointing at closed ports, validated at startup but never connected to, and nothing
/// is migrated or seeded at startup (SPEC-0065), whatever the environment.
/// </summary>
internal static class BuildTimeDocument
{
    private const string GeneratorAssemblyName = "GetDocument.Insider";

    private static readonly Dictionary<string, string?> PlaceholderSettings = new(StringComparer.Ordinal)
    {
        ["ConnectionStrings:apogeu"] = "Host=127.0.0.1;Port=1;Database=apogeu;Username=apogeu;Timeout=1",
        ["ConnectionStrings:redis"] = "127.0.0.1:1,abortConnect=false,connectTimeout=200",
        ["Database:MigrateOnStartup"] = "false",
        ["Operators:Demo:Enabled"] = "false",
    };

    public static bool IsGenerating =>
        string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, GeneratorAssemblyName, StringComparison.Ordinal);

    /// <summary>Supplies the placeholders when the Host runs only to generate the document; no-op otherwise.</summary>
    public static IHostApplicationBuilder UseBuildTimeDocumentConfiguration(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (IsGenerating)
        {
            builder.Configuration.AddInMemoryCollection(PlaceholderSettings);
        }

        return builder;
    }
}
