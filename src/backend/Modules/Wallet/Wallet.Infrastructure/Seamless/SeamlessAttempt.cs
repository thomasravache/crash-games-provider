using Apogeu.Wallet.Contracts;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>
/// One attempt of a credit or a rollback at the operator wallet: its <see cref="Result"/> and whether the operator
/// <see cref="Settled"/> it with a definitive answer (OK, DUPLICATE, TRANSACTION_NOT_FOUND or PLAYER_NOT_FOUND). Anything
/// else — no answer, an answer outside the contract, the configuration missing or invalid — keeps it pending (SPEC-0046 G4).
/// </summary>
internal readonly record struct SeamlessAttempt(WalletResult Result, bool Settled);
