namespace Apogeu.Operators.Infrastructure.Persistence;

/// <summary>A row of <c>operators.operators</c>: an operator (B2B customer) and the sala its players join (SPEC-0009).</summary>
public sealed class OperatorRecord
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string SalaId { get; set; }
}
