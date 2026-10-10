namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// The Data Protection key ring of the instances of one test (the Fairness seeds encrypted by one instance are read by the
/// next leader): a temp directory deleted when the last instance that shares it is disposed.
/// </summary>
internal sealed class SharedKeys
{
    private int _holders = 1;

    private SharedKeys(DirectoryInfo directory) => Directory = directory;

    public DirectoryInfo Directory { get; }

    public static SharedKeys Create() => new(System.IO.Directory.CreateTempSubdirectory("apogeu-betting-keys-"));

    public SharedKeys Share()
    {
        Interlocked.Increment(ref _holders);
        return this;
    }

    public void Release()
    {
        if (Interlocked.Decrement(ref _holders) > 0)
        {
            return;
        }

        try
        {
            Directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must not fail the run.
        }
    }
}
