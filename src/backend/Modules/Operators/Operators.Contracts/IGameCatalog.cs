using Apogeu.BuildingBlocks.Contracts;
namespace Apogeu.Operators.Contracts;

/// <summary>The games of the provider and which operators may launch them (SPEC-0020 §6, contract v1).</summary>
public interface IGameCatalog
{
    /// <summary>The game <paramref name="code"/>, or <see langword="null"/> when the catalog has no such game.</summary>
    GameDefinition? Find(GameCode code);

    /// <summary>Whether the operator <paramref name="op"/> may launch the game <paramref name="code"/>.</summary>
    bool IsEnabledFor(OperatorId op, GameCode code);
}
