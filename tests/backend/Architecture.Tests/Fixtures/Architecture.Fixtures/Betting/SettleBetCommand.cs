namespace Apogeu.Betting.Contracts;

/// <summary>Public contract of the Betting module, used by the Wallet fixture to close a module cycle (UT-01).</summary>
public sealed record SettleBetCommand(Guid BetId);
