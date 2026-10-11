// Local validation of the bet panel (SPEC-0026 §6, UT-02, UT-06): outside the limits the button is disabled with the
// reason; the server still has the final word. Also the "Recarregar demo" rule.

import type { Money, Multiplier } from "@apogeu/contracts";

import type { BetLimits } from "./bets-state";
import {
  addMoney,
  compareMoney,
  compareMultiplier,
  formatMoney,
  parseMoneyInput,
  parseMultiplierInput,
  subtractMoney,
} from "./money";

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
  switch (reason) {
    case "LIMITS_UNKNOWN":
      return "Carregando os limites de aposta…";
    case "INVALID_AMOUNT":
      return "Informe um valor de aposta válido.";
    case "BELOW_MIN":
      return limits === null
        ? "Valor abaixo da aposta mínima."
        : `Aposta mínima: ${formatMoney(limits.minBet)}`;
    case "ABOVE_MAX":
      return limits === null
        ? "Valor acima da aposta máxima."
        : `Aposta máxima: ${formatMoney(limits.maxBet)}`;
    case "AUTO_TARGET_INVALID":
      return "Informe um alvo válido para o cash-out automático.";
    case "AUTO_TARGET_TOO_LOW":
      return "O alvo do cash-out automático deve ser de pelo menos 1,01x.";
  }
}

function invalid(reason: BetValidationReason, limits: BetLimits | null): BetValidation {
  return { ok: false, reason, message: betValidationMessage(reason, limits) };
}

/** Validates a draft against the limits and the 1.01x minimum target. */
export function validateBet(draft: BetDraft, limits: BetLimits | null): BetValidation {
  const amount = parseMoneyInput(draft.amount);
  if (amount === null) return invalid("INVALID_AMOUNT", limits);
  if (limits === null) return invalid("LIMITS_UNKNOWN", limits);
  if (compareMoney(amount, limits.minBet) < 0) return invalid("BELOW_MIN", limits);
  if (compareMoney(amount, limits.maxBet) > 0) return invalid("ABOVE_MAX", limits);
  if (draft.autoCashOutEnabled) {
    const target = parseMultiplierInput(draft.autoCashOutTarget);
    if (target === null) return invalid("AUTO_TARGET_INVALID", limits);
    if (compareMultiplier(target, minAutoCashOut) < 0)
      return invalid("AUTO_TARGET_TOO_LOW", limits);
  }
  return { ok: true };
}

/** The amount after a − (step −1) or + (step +1), kept inside [minBet, maxBet] when the limits are known. */
export function stepAmount(amount: Money, direction: -1 | 1, limits: BetLimits | null): Money {
  const next = direction === 1 ? addMoney(amount, amountStep) : subtractMoney(amount, amountStep);
  if (limits === null) return next;
  if (compareMoney(next, limits.minBet) < 0) return limits.minBet;
  if (compareMoney(next, limits.maxBet) > 0) return limits.maxBet;
  return next;
}

/** "Recarregar demo" is offered only when the balance is below the minimum bet (SPEC-0021@4). */
export function shouldOfferRecharge(balance: Money | null, limits: BetLimits | null): boolean {
  return balance !== null && limits !== null && compareMoney(balance, limits.minBet) < 0;
}
