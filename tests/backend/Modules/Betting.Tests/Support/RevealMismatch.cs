namespace Apogeu.Betting.Tests.Support;

/// <summary>Switch shared by every scope's <see cref="AnnullingFairness"/>: armed by the test, consumed by the next reveal.</summary>
internal sealed class RevealMismatch
{
    private int _armed;

    public void ArmNextReveal() => Volatile.Write(ref _armed, 1);

    /// <summary>Whether the switch was armed; disarms it.</summary>
    public bool Consume() => Interlocked.Exchange(ref _armed, 0) == 1;
}
