using Xunit;

namespace Apogeu.Repository.Tests.Support;

/// <summary>
/// Gives the integration tests a disposable copy of the repository so the real working tree is never mutated.
/// The copy holds exactly the files git would consider part of the project: tracked files plus untracked files
/// that are not ignored (bin/obj/node_modules are always left out).
/// </summary>
public sealed class RepositoryCopyFixture : IAsyncLifetime
{
    private static readonly string[] ExcludedSegments = ["bin", "obj", "node_modules", ".git"];

    private readonly List<string> _createdDirectories = [];
    private readonly Lazy<Task<ProcessResult>> _frontendInstall;
    private string[] _files = [];

    public RepositoryCopyFixture()
    {
        _frontendInstall = new Lazy<Task<ProcessResult>>(
            () => ProcessRunner.PnpmAsync(
                SharedCopyPath,
                ["-C", "src/frontend", "install", "--frozen-lockfile"],
                CancellationToken.None),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Copy shared by the build/format/lint tests. Tests that add files must remove them afterwards.</summary>
    public string SharedCopyPath { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _files = await ListRepositoryFilesAsync().ConfigureAwait(false);
        SharedCopyPath = CopyRepository();
    }

    public ValueTask DisposeAsync()
    {
        foreach (var directory in _createdDirectories)
        {
            TryDelete(directory);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Creates another independent copy (for tests that need their own git repository, for example).</summary>
    public string CreateIsolatedCopy() => CopyRepository();

    /// <summary>Creates an empty scratch directory that is deleted when the fixture is disposed.</summary>
    public string CreateScratchDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "apogeu-repo-scratch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        lock (_createdDirectories)
        {
            _createdDirectories.Add(directory);
        }

        return directory;
    }

    /// <summary>Runs `pnpm -C src/frontend install --frozen-lockfile` once in the shared copy and caches the result.</summary>
    public Task<ProcessResult> EnsureFrontendInstalledAsync() => _frontendInstall.Value;

    private string CopyRepository()
    {
        var destination = CreateScratchDirectory();
        foreach (var relativePath in _files)
        {
            var source = Path.Combine(RepositoryRoot.Path, relativePath);
            if (!File.Exists(source))
            {
                continue; // tracked but deleted in the working tree
            }

            var target = Path.Combine(destination, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true); // keeps the executable bit of hooks
        }

        return destination;
    }

    private static async Task<string[]> ListRepositoryFilesAsync()
    {
        var result = await ProcessRunner
            .GitAsync(RepositoryRoot.Path, ["ls-files", "-z", "--cached", "--others", "--exclude-standard"], CancellationToken.None)
            .ConfigureAwait(false);

        IEnumerable<string> paths = result.Succeeded
            ? result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            : Directory
                .EnumerateFiles(RepositoryRoot.Path, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(RepositoryRoot.Path, file));

        return paths
            .Select(path => path.Replace('\\', '/').Trim('\n', '\r'))
            .Where(path => path.Length > 0)
            .Where(path => !path.Split('/').Any(segment => ExcludedSegments.Contains(segment, StringComparer.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must not fail the run.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
