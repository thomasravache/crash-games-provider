// Money helpers of the bet panel (SPEC-0026 §6, UT-04, ADR-0008). Values travel and live as decimal strings
// ("2990.5", "24.70"); arithmetic is done on integer cents (bigint), never with the floating point number type, so
// nothing loses a cent.

import type { Money, Multiplier } from "@apogeu/contracts";

/** The decimal string scalars of the contracts, re-exported for the HUD (which does not depend on contracts). */
export type { Money, Multiplier } from "@apogeu/contracts";

/** Non-negative decimal with at most two places. */
const decimalPattern = /^\d+(?:\.\d{1,2})?$/u;

/** Integer cents of a valid decimal string ("2990.5" → 299050n). */
function toCents(value: string): bigint {
  const [units = "0", fraction = ""] = value.split(".");
  return BigInt(units) * 100n + BigInt(fraction.padEnd(2, "0"));
}

/** Canonical two-place decimal string of integer cents, floored at zero (299050n → "2990.50"). */
function fromCents(cents: bigint): string {
  const positive = cents < 0n ? 0n : cents;
  return `${(positive / 100n).toString()}.${(positive % 100n).toString().padStart(2, "0")}`;
}

function sign(difference: bigint): -1 | 0 | 1 {
  if (difference < 0n) return -1;
  return difference > 0n ? 1 : 0;
}

/** Whether the text is a non-negative decimal with at most two places. */
export function isMoney(value: string): value is Money {
  return decimalPattern.test(value);
}

/** A decimal string with at most two places ("10", "10.5", "10.50"); throws RangeError otherwise. */
export function toMoney(value: string): Money {
  if (!isMoney(value)) throw new RangeError(`Not a money value: "${value}".`);
  return value;
}

/** A decimal string with at most two places ("1.01"); throws RangeError otherwise. */
export function toMultiplier(value: string): Multiplier {
  if (!decimalPattern.test(value)) throw new RangeError(`Not a multiplier: "${value}".`);
  return value as Multiplier;
}

/** Canonical two-place form sent to the server: "2990.5" → "2990.50", "10" → "10.00". */
export function normalizeMoney(value: Money): Money {
  return fromCents(toCents(toMoney(value))) as Money;
}

/** pt-BR currency text: "2990.5" → "R$ 2.990,50", "24.70" → "R$ 24,70" (plain ASCII space, exact digits). */
export function formatMoney(value: Money): string {
  const [units = "0", fraction = "00"] = normalizeMoney(value).split(".");
  const grouped = units.replace(/\B(?=(\d{3})+(?!\d))/gu, ".");
  return `R$ ${grouped},${fraction}`;
}

/**
 * Decimal text typed by the player ("10,50", "1.000,01", "10.50", "R$ 10,50") → canonical two-place string, or null.
 * With a comma, dots are thousand separators; without one, dots before groups of three digits are thousand separators
 * and a single dot followed by one or two digits is the decimal point.
 */
function parseDecimalInput(text: string, stripSuffix?: RegExp): string | null {
  let value = text
    .trim()
    .replace(/^R\$\s*/u, "")
    .trim();
  if (stripSuffix !== undefined) value = value.replace(stripSuffix, "").trim();
  if (value === "") return null;
  if (value.includes(",")) {
    if (!/^(?:\d{1,3}(?:\.\d{3})+|\d+),\d{0,2}$/u.test(value)) return null;
    value = value.replaceAll(".", "").replace(",", ".");
  } else if (/^\d{1,3}(?:\.\d{3})+$/u.test(value)) {
    value = value.replaceAll(".", "");
  }
  value = value.replace(/\.$/u, "");
  return decimalPattern.test(value) ? fromCents(toCents(value)) : null;
}

/** Amount typed by the player ("10,50", "10.50", "R$ 10,50") → Money, or null when it is not a valid amount. */
export function parseMoneyInput(text: string): Money | null {
  return parseDecimalInput(text) as Money | null;
}

/** Multiplier typed by the player ("2,5", "2.50", "2,50x") → Multiplier, or null when it is not valid. */
export function parseMultiplierInput(text: string): Multiplier | null {
  return parseDecimalInput(text, /x$/iu) as Multiplier | null;
}

/** -1, 0 or 1, comparing the decimal values exactly. */
export function compareMoney(a: Money, b: Money): -1 | 0 | 1 {
  return sign(toCents(a) - toCents(b));
}

/** -1, 0 or 1, comparing the decimal values exactly. */
export function compareMultiplier(a: Multiplier, b: Multiplier): -1 | 0 | 1 {
  return sign(toCents(a) - toCents(b));
}

export function addMoney(a: Money, b: Money): Money {
  return fromCents(toCents(a) + toCents(b)) as Money;
}

/** a − b, floored at 0.00. */
export function subtractMoney(a: Money, b: Money): Money {
  return fromCents(toCents(a) - toCents(b)) as Money;
}

/**
 * Payout shown on "RETIRAR R$ x" for the current multiplier: floor(amount × multiplier × 100)/100 (the rule of
 * SPEC-0023@2), capped at `maxWin` when given. Display only: the server decides the paid value.
 */
export function cashOutPreview(amount: Money, multiplier: Multiplier, maxWin?: Money): Money {
  // amount cents × multiplier hundredths / 100 = payout cents, floored (both factors are non-negative).
  const payout = (toCents(amount) * toCents(multiplier)) / 100n;
  const cap = maxWin === undefined ? undefined : toCents(maxWin);
  return fromCents(cap !== undefined && payout > cap ? cap : payout) as Money;
}
