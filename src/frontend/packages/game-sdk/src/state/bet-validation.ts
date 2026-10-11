// Local validation of the bet panel (SPEC-0026 §6, UT-02, UT-06): outside the limits the button is disabled with the
// reason; the server still has the final word. Also the "Recarregar demo" rule.

import type { Money, Multiplier } from "@apogeu/contracts";

import { notImplemented } from "../not-implemented";
import type { BetLimits } from "./bets-state";

/** Smallest automatic cash-out target (SPEC-0022@3 AUTO_CASHOUT_INVALID below it). */
export const minAutoCashOut = "1.01" as Multiplier;

/** Step of the − / + buttons of the amount (R$ 1,00). */
export const amountStep = "1.00" as Money;

/** Quick chips of the panel: R$ 1, 5, 10 and 50. */
export const quickChips: readonly Money[] = ["1.00", "5.00", "10.00", "50.00"] as Money[];

/** Why the bet button is disabled. */
export type BetValidationReason =
  | "LIMITS_UNKNOWN"
  | "INVALID_AMOUNT"
  | "BELOW_MIN"
  | "ABOVE_MAX"
  | "AUTO_TARGET_INVALID"
  | "AUTO_TARGET_TOO_LOW";

export type BetValidation =
  | { readonly ok: true }
  | { readonly ok: false; readonly reason: BetValidationReason; readonly message: string };

/** What the player set on a panel before betting. */
export interface BetDraft {
  /** Amount as typed (pt-BR or decimal text); invalid text is INVALID_AMOUNT. */
  readonly amount: string;
  readonly autoCashOutEnabled: boolean;
  /** Target as typed ("2,00"); only checked when the automatic cash-out is enabled. */
  readonly autoCashOutTarget: string;
}

/**
 * pt-BR reason shown next to the disabled button (the `message` of BetValidation), for example "Aposta mínima:
 * R$ 1,00" or "O alvo do cash-out automático deve ser de pelo menos 1,01x".
 */
export function betValidationMessage(
  reason: BetValidationReason,
  limits: BetLimits | null,
): string {
  return notImplemented(reason, limits);
}

/** Validates a draft against the limits and the 1.01x minimum target. */
export function validateBet(draft: BetDraft, limits: BetLimits | null): BetValidation {
  return notImplemented(draft, limits);
}

/** The amount after a − (step −1) or + (step +1), kept inside [minBet, maxBet] when the limits are known. */
export function stepAmount(amount: Money, direction: -1 | 1, limits: BetLimits | null): Money {
  return notImplemented(amount, direction, limits);
}

/** "Recarregar demo" is offered only when the balance is below the minimum bet (SPEC-0021@4). */
export function shouldOfferRecharge(balance: Money | null, limits: BetLimits | null): boolean {
  return notImplemented(balance, limits);
}
