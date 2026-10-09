namespace Apogeu.Fairness.Infrastructure.Persistence;

/// <summary>
/// A row of <c>fairness.checkpoints</c>: the link h_<see cref="LinkIndex"/> of a chain encrypted with Data Protection
/// (ADR-0014). Link 0 is the chain origin h_0; the others are every 10,000 links.
/// </summary>
internal sealed class CheckpointRecord
{
    public Guid ChainId { get; set; }

    public int LinkIndex { get; set; }

    public byte[] ProtectedLink { get; set; } = [];
}
