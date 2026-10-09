namespace Apogeu.Operators.Features.Sessions;

/// <summary>What the game shows about the player: only the optional nickname (SPEC-0009 §5).</summary>
public sealed record SessionPlayer(string? Nickname);
