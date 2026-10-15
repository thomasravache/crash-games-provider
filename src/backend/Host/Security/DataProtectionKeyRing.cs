using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Apogeu.Host.Security;

/// <summary>
/// The key ring of the Host's Data Protection (ADR-0014; it encrypts the chain origins of Fairness, SPEC-0007). With
/// <c>DataProtection:KeysPath</c> (the volume <c>dataprotection-keys</c> of the production compose, SPEC-0065) the keys are
/// kept in that directory under a fixed application name, so a restart or a redeploy (another container, another content
/// root) still reads what was encrypted before. A directory that does not accept writes fails the startup, naming it
/// (Emenda v3), instead of the first encryption of a seed chain. Without it the framework defaults stay, as in the local run.
/// </summary>
internal static class DataProtectionKeyRing
{
    public const string KeysPathKey = "DataProtection:KeysPath";

    /// <summary>Application name shared by every instance that reads the same key ring.</summary>
    public const string ApplicationName = "Apogeu";

    /// <summary>
    /// Registers the Data Protection with the key directory read from the configuration when the options are first built
    /// (the same as <c>PersistKeysToFileSystem</c> and <c>SetApplicationName</c>, applied only when the directory is set).
    /// </summary>
    public static IServiceCollection AddDataProtectionKeyRing(this IServiceCollection services)
    {
        services.AddDataProtection();
        services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, ILoggerFactory>((options, configuration, loggers) =>
        {
            if (KeysPath(configuration) is { } path)
            {
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(path), loggers);
            }
        });
        services.AddOptions<DataProtectionOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            if (KeysPath(configuration) is not null)
            {
                options.ApplicationDiscriminator = ApplicationName;
            }
        });

        return services;
    }

    /// <summary>
    /// Fails the startup when the configured key directory cannot be created or written to, with a message naming it;
    /// no-op without <c>DataProtection:KeysPath</c>.
    /// </summary>
    public static void EnsureKeysPathWritable(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (KeysPath(configuration) is not { } path)
        {
            return;
        }

        var probe = Path.Combine(path, $".write-probe-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(path);
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new InvalidOperationException(
                $"The Data Protection key directory '{path}' ({KeysPathKey}) does not accept writes by this process: the keys "
                + "that encrypt the seed chains cannot be kept. Give the directory (the volume dataprotection-keys) to the "
                + "user of the API.",
                exception);
        }
    }

    private static string? KeysPath(IConfiguration configuration) =>
        configuration[KeysPathKey] is { } path && !string.IsNullOrWhiteSpace(path) ? path.Trim() : null;
}
