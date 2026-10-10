using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Wallet.Features.Demo;

/// <summary><c>200</c> of <c>POST /api/v1/wallet/demo/recharge</c> (SPEC-0021 §6): the balance after the recharge.</summary>
/// <param name="Balance">The opening demo amount the balance was brought back to.</param>
/// <param name="Currency">ISO 4217 code of the session currency.</param>
public sealed record RechargeResponse(Money Balance, string Currency);
