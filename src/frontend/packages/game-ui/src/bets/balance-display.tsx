// Confirmed balance with the DEMO badge ("R$ 2.990,00 · DEMO") and the "Recarregar demo" button, visible only while
// the balance is below the minimum bet (SPEC-0026 §6, UT-06). Replaces the fixed demo balance of SPEC-0010.

import type { BetLimits, Money } from "@apogeu/game-sdk/state";
import type { JSX } from "react";

import { notImplemented } from "./not-implemented";

export interface BalanceDisplayProps {
  /** Balance confirmed by the server; null until the first read. */
  readonly balance: Money | null;
  readonly limits: BetLimits | null;
  /** Recharge request in flight (the button is disabled meanwhile). */
  readonly recharging: boolean;
  readonly onRecharge: () => void;
}

export function BalanceDisplay(props: BalanceDisplayProps): JSX.Element {
  return notImplemented(props);
}
