using System.Runtime.Versioning;
using Apogeu.Fairness.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Fairness.Tests.Integration;

/// <summary>
/// SPEC-0065 IT-03 (b) (Emenda v3): a key directory the Host cannot write to is found at startup, with a message that names
/// it, instead of on the first encryption of a seed chain (when the rounds would already be failing).
/// </summary>
[Collection(FairnessHostDependencies.Name)]
public sealed class UnwritableKeysPathTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dado o Host com DataProtection:KeysPath num diretório sem escrita, quando sobe, então falha na subida citando o caminho")]
    [Trait("Spec", "SPEC-0065:IT-03")]
    [Trait("Category", "Integration")]
    [UnsupportedOSPlatform("windows")]
    public async Task GivenHostWithUnwritableKeysPathWhenStartingThenStartupFailsNamingThePath()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        using var keys = new TemporaryKeyDirectory();
        File.SetUnixFileMode(keys.Directory.FullName, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            // Control: the directory is really not writable by this process (root would write anyway).
            Assert.SkipWhen(CanWrite(keys.Directory), "This user can write to a read-only directory (root): the case cannot be reproduced.");

            FairnessApiHost? started = null;
            Exception? failure = null;
            try
            {
                started = await FairnessApiHost.StartAsync(database, redis.ConnectionString, keys.Directory);
            }
#pragma warning disable CA1031 // The startup failure, whatever its type, is what the test observes.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                failure = exception;
            }

            if (started is not null)
            {
                await started.DisposeAsync();
            }

            Assert.True(failure is not null, $"The Host started with DataProtection:KeysPath not writable ({keys.Directory.FullName}).");
            Assert.True(
                failure.ToString().Contains(keys.Directory.FullName, StringComparison.Ordinal),
                $"The startup failure does not name the key directory {keys.Directory.FullName}: {failure}");
        }
        finally
        {
            File.SetUnixFileMode(keys.Directory.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static bool CanWrite(DirectoryInfo directory)
    {
        var probe = Path.Combine(directory.FullName, "probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
