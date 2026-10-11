using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;

namespace Apogeu.Rounds.Features.Salas;

/// <summary><see cref="ISalaCaps"/> over the configured rooms: the cap <see cref="RoundParameters"/> gives each new round.</summary>
internal sealed class SalaCaps(SalaCatalog salas) : ISalaCaps
{
    public Multiplier? CapOf(string salaId) =>
        salas.TryGet(salaId, out var sala) ? new Multiplier(RoundParameters.Of(sala).Cap) : null;
}
