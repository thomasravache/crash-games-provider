namespace Apogeu.Fairness.Tests.Support;

/// <summary>A temporary directory for the Data Protection key ring of a test, deleted at the end.</summary>
internal sealed class TemporaryKeyDirectory : IDisposable
{
    public DirectoryInfo Directory { get; } = System.IO.Directory.CreateTempSubdirectory("apogeu-fairness-keys-");

    public void Dispose() => Directory.Delete(recursive: true);
}
