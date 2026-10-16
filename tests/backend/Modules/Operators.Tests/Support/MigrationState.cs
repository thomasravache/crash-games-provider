namespace Apogeu.Operators.Tests.Support;

/// <summary>Migrations of one DbContext: its schema, the applied ones and the pending ones.</summary>
internal sealed record MigrationState(string Context, string? Schema, IReadOnlyList<string> Applied, IReadOnlyList<string> Pending);
