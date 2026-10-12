// Confirmed balance with the DEMO badge ("R$ 2.990,00 · DEMO") and the "Recarregar demo" button, visible only while
// the balance is below the minimum bet (SPEC-0026 §6, UT-06). Replaces the fixed demo balance of SPEC-0010. Empty
// until the first read: never "R$ 0,00" nor a false recharge.

import {
  betErrorMessage,
  formatMoney,
  shouldOfferRecharge,
  type BetErrorCode,
  type BetLimits,
  type Money,
} from "@apogeu/game-sdk/state";
import type { JSX } from "react";

import { balanceText, betTestIds } from "./bet-text";

export interface BalanceDisplayProps {
  /** Balance confirmed by the server; null until the first read. */
  readonly balance: Money | null;
  readonly limits: BetLimits | null;
  /** Recharge request in flight (the button is disabled meanwhile). */
  readonly recharging: boolean;
  readonly onRecharge: () => void;
  /** Code of the last refused recharge (RECHARGE_NOT_ALLOWED, WALLET_UNAVAILABLE…), shown as an alert; null when none. */
  readonly rechargeError?: BetErrorCode | null;
}

export function BalanceDisplay({
  balance,
  limits,
  recharging,
  onRecharge,
  rechargeError,
}: BalanceDisplayProps): JSX.Element {
  return (
    <div className="flex flex-col items-end gap-1">
      <p className="text-sm font-semibold tabular-nums">
        <span className="sr-only">{`${balanceText.label}: `}</span>
        <span data-testid={betTestIds.balance}>
          {balance === null ? "" : balanceText.value(formatMoney(balance))}
        </span>
      </p>
      {shouldOfferRecharge(balance, limits) ? (
        <button
          type="button"
          data-testid={betTestIds.recharge}
          disabled={recharging}
          onClick={onRecharge}
          className="min-h-8 rounded-full border border-hud-accent px-3 text-xs font-semibold text-hud-accent hover:bg-hud-accent/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-hud-accent"
        >
          {balanceText.recharge}
        </button>
      ) : null}
      {rechargeError === undefined || rechargeError === null ? null : (
        <p role="alert" className="max-w-56 text-right text-xs text-hud-text">
          {betErrorMessage(rechargeError)}
        </p>
      )}
    </div>
  );
}
