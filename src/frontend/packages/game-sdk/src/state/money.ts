// Money helpers of the bet panel (SPEC-0026 §6, UT-04, ADR-0008). Values travel and live as decimal strings
// ("2990.5", "24.70"); arithmetic is done on integer cents (bigint), never with Number, so nothing loses a cent.

import type { Money, Multiplier } from "@apogeu/contracts";

import { notImplemented } from "../not-implemented";

/** The decimal string scalars of the contracts, re-exported for the HUD (which does not depend on contracts). */
export type { Money, Multiplier } from "@apogeu/contracts";

/** A decimal string with at most two places ("10", "10.5", "10.50"); throws RangeError otherwise. */
export function toMoney(value: string): Money {
  return notImplemented(value);
}

/** A decimal string with at most two places ("1.01"); throws RangeError otherwise. */
export function toMultiplier(value: string): Multiplier {
  return notImplemented(value);
}

/** Whether the text is a non-negative decimal with at most two places. */
export function isMoney(value: string): value is Money {
  return notImplemented(value);
}

/** Canonical two-place form sent to the server: "2990.5" → "2990.50", "10" → "10.00". */
export function normalizeMoney(value: Money): Money {
  return notImplemented(value);
}

/** pt-BR currency text: "2990.5" → "R$ 2.990,50", "24.70" → "R$ 24,70" (no Number on the way). */
export function formatMoney(value: Money): string {
  return notImplemented(value);
}

/** Amount typed by the player ("10,50", "10.50", "R$ 10,50") → Money, or null when it is not a valid amount. */
export function parseMoneyInput(text: string): Money | null {
  return notImplemented(text);
}

/** Multiplier typed by the player ("2,5", "2.50", "2,50x") → Multiplier, or null when it is not valid. */
export function parseMultiplierInput(text: string): Multiplier | null {
  return notImplemented(text);
}

/** -1, 0 or 1, comparing the decimal values exactly. */
export function compareMoney(a: Money, b: Money): -1 | 0 | 1 {
  return notImplemented(a, b);
}

/** -1, 0 or 1, comparing the decimal values exactly. */
export function compareMultiplier(a: Multiplier, b: Multiplier): -1 | 0 | 1 {
  return notImplemented(a, b);
}

export function addMoney(a: Money, b: Money): Money {
  return notImplemented(a, b);
}

/** a − b, floored at 0.00. */
export function subtractMoney(a: Money, b: Money): Money {
  return notImplemented(a, b);
}

/**
 * Payout shown on "RETIRAR R$ x" for the current multiplier: floor(amount × multiplier × 100)/100 (the rule of
 * SPEC-0023@2), capped at `maxWin` when given. Display only: the server decides the paid value.
 */
export function cashOutPreview(amount: Money, multiplier: Multiplier, maxWin?: Money): Money {
  return notImplemented(amount, multiplier, maxWin);
}
