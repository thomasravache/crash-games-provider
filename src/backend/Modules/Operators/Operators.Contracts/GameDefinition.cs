using Apogeu.BuildingBlocks.Contracts;
namespace Apogeu.Operators.Contracts;

/// <summary>A game of the catalog (SPEC-0020 §6): what the client loads, its theme, the sala default and its currencies.</summary>
/// <param name="Code">Code of the game (for example <c>pipa</c>).</param>
/// <param name="Name">Display name (for example <c>Pipa</c>).</param>
/// <param name="ClientApp">Front-end application that renders the game (for example <c>game-pipa</c>).</param>
/// <param name="Theme">Visual theme of the client (for example <c>pipa</c>).</param>
/// <param name="DefaultSala">Default configuration of new salas of the game.</param>
/// <param name="Currencies">Currencies the game accepts.</param>
public sealed record GameDefinition(
    GameCode Code,
    string Name,
    string ClientApp,
    string Theme,
    SalaDefaults DefaultSala,
    IReadOnlyList<Currency> Currencies);
