namespace Apogeu.Betting.Features;

/// <summary>Result of a command of the module: its <paramref name="Value"/>, or the contract error <paramref name="Code"/> it was refused with.</summary>
public sealed record BettingOutcome<T>(T? Value, string? Code)
    where T : class;
