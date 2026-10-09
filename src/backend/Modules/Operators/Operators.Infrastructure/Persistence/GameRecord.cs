namespace Apogeu.Operators.Infrastructure.Persistence;

/// <summary>
/// A row of <c>operators.games</c>: a game of the catalog with the default configuration of its new salas and the currencies
/// it accepts (SPEC-0020 §6).
/// </summary>
internal sealed class GameRecord
{
    public required string Code { get; set; }

    public required string Name { get; set; }

    public required string ClientApp { get; set; }

    public required string Theme { get; set; }

    public decimal Rtp { get; set; }

    public decimal Cap { get; set; }

    public double GrowthK { get; set; }

    public int BettingSeconds { get; set; }

    public int PauseSeconds { get; set; }

    /// <summary>ISO 4217 codes of the currencies the game accepts.</summary>
    public List<string> Currencies { get; set; } = [];
}
