namespace Apogeu.Rounds.Infrastructure.Persistence;

/// <summary>A row of <c>rounds.chains</c>: the public data of a Fairness chain played by a room.</summary>
internal sealed class ChainRecord
{
    public Guid Id { get; set; }

    public string SalaId { get; set; } = string.Empty;

    public string TerminalHash { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    public int Length { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
