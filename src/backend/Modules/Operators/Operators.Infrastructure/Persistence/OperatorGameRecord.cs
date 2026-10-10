namespace Apogeu.Operators.Infrastructure.Persistence;

/// <summary>A row of <c>operators.operator_games</c>: whether the operator may launch the game (SPEC-0020 §2).</summary>
internal sealed class OperatorGameRecord
{
    public required string OperatorId { get; set; }

    public required string GameCode { get; set; }

    public bool Enabled { get; set; }
}
