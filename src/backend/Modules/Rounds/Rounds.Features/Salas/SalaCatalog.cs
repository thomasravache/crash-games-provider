using System.Diagnostics.CodeAnalysis;
using Apogeu.Rounds.Domain;
using Microsoft.Extensions.Options;

namespace Apogeu.Rounds.Features.Salas;

/// <summary>The configured rooms (<c>Rounds:Salas:{id}</c>); without any configured room, the room <c>demo</c> with the defaults.</summary>
public sealed class SalaCatalog
{
    /// <summary>Id of the room seeded when the configuration declares none (SPEC-0008 §2).</summary>
    public const string DemoSalaId = "demo";

    private readonly Dictionary<string, SalaConfig> _salas;

    public SalaCatalog(IOptions<RoundsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = options.Value.Salas.Count > 0
            ? options.Value.Salas
            : new Dictionary<string, SalaOptions>(StringComparer.Ordinal) { [DemoSalaId] = new SalaOptions() };
        _salas = configured.ToDictionary(pair => pair.Key, pair => ToConfig(pair.Key, pair.Value), StringComparer.Ordinal);
    }

    public IReadOnlyCollection<SalaConfig> All => _salas.Values;

    public bool TryGet(string? salaId, [NotNullWhen(true)] out SalaConfig? sala)
    {
        sala = null;
        return salaId is not null && _salas.TryGetValue(salaId, out sala);
    }

    private static SalaConfig ToConfig(string id, SalaOptions options) =>
        new(id, options.Rtp, options.Cap, options.GrowthK, options.BettingDuration, options.CooldownDuration, options.ChainLength);
}
