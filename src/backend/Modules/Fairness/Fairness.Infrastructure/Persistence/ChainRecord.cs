namespace Apogeu.Fairness.Infrastructure.Persistence;

/// <summary>
/// A row of <c>fairness.chains</c>: the public data of a chain (terminal hash h_N, salt, length) and the number of rounds
/// already issued. No secret link is stored here; they are in <see cref="CheckpointRecord"/>, encrypted.
/// </summary>
internal sealed class ChainRecord
{
    public Guid Id { get; set; }

    public Guid SalaId { get; set; }

    /// <summary>Creation order (identity): the active chain of a room is its latest one.</summary>
    public long Sequence { get; set; }

    public int Length { get; set; }

    /// <summary>Rounds issued so far: the next round is <c>IssuedRounds + 1</c>, at most <see cref="Length"/>.</summary>
    public int IssuedRounds { get; set; }

    public byte[] TerminalHash { get; set; } = [];

    public byte[] Salt { get; set; } = [];
}
