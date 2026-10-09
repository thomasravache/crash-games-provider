namespace Apogeu.Rounds.Features;

/// <summary>Configuration section <c>Rounds</c>: the rooms by id (<c>Rounds:Salas:{id}</c>, SPEC-0008 §4).</summary>
public sealed class RoundsOptions
{
    public const string SectionName = "Rounds";

    public IDictionary<string, SalaOptions> Salas { get; } = new Dictionary<string, SalaOptions>(StringComparer.Ordinal);
}
