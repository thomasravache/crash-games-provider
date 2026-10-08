namespace Apogeu.Repository.Tests.Support;

/// <summary>Locates the repository root (the folder that contains docs/specs/sdd-config.yml).</summary>
internal static class RepositoryRoot
{
    private static readonly Lazy<string> Located = new(Locate);

    public static string Path => Located.Value;

    public static string Combine(params string[] relativeSegments) =>
        System.IO.Path.Combine([Path, .. relativeSegments]);

    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var marker = System.IO.Path.Combine(directory.FullName, "docs", "specs", "sdd-config.yml");
            if (File.Exists(marker))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Repository root not found: no parent of '{AppContext.BaseDirectory}' contains docs/specs/sdd-config.yml.");
    }
}
