using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Wallet.Features.Balances;

/// <summary><c>200</c> of <c>GET /api/v1/wallet/balance</c> (SPEC-0021 §6): the session player's balance, currency and wallet mode.</summary>
/// <param name="Balance">Available amount.</param>
/// <param name="Currency">ISO 4217 code of the session currency.</param>
/// <param name="Mode">Adapter of the wallet: <c>demo</c>.</param>
public sealed record BalanceResponse(Money Balance, string Currency, string Mode);
