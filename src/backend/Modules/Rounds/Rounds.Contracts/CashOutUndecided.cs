namespace Apogeu.Rounds.Contracts;

/// <summary>
/// The leader cannot decide yet (SPEC-0023 §6, review G4): it does not know the round and the round has not ended (e.g. a
/// leader taking over). The requester tries again; nothing about the crash is told.
/// </summary>
public sealed record CashOutUndecided : CashOutDecision;
