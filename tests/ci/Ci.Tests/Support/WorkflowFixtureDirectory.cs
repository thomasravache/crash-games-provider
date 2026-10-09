namespace Apogeu.Ci.Tests.Support;

/// <summary>Temporary directory holding one workflow fixture, deleted on dispose.</summary>
public sealed class WorkflowFixtureDirectory : IDisposable
{
    private WorkflowFixtureDirectory(string path) => Path = path;

    public string Path { get; }

    public static WorkflowFixtureDirectory Create(string workflowYaml)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "apogeu-ci-workflows-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        File.WriteAllText(System.IO.Path.Combine(path, "fixture.yml"), workflowYaml);
        return new WorkflowFixtureDirectory(path);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must not fail the run.
        }
    }
}
