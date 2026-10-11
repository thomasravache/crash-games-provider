// Bet events the bets store reduces (SPEC-0026 §6). They carry the data of the BetsHub messages (SPEC-0022@3,
// SPEC-0023@2, SPEC-0024@2), of the wallet (SPEC-0021@4) and the round phases, but are state types, not transport
// types: the bets client maps each message to one event.

import type { Money, Multiplier } from "@apogeu/contracts";

import type { BetErrorCode } from "./bet-errors";
import type { BetLimits, OpenBet, PanelNumber } from "./bets-state";

export type BetsEvent =
  /** GetBetLimits answered. */
  | { readonly type: "limits"; readonly limits: BetLimits }
  /** Balance read from the wallet (GET balance or demo recharge). */
  | { readonly type: "balance"; readonly balance: Money; readonly currency: string }
  /** Reconnection reload: balance from the wallet; open bets from GET /api/v1/me/bets, or null when it answered 404. */
  | {
      readonly type: "reloaded";
      readonly balance: Money;
      readonly currency: string;
      readonly openBets: readonly OpenBet[] | null;
    }
  /** The player asked to bet on a panel (PlaceBet sent). */
  | {
      readonly type: "placeRequested";
      readonly panel: PanelNumber;
      readonly clientBetId: string;
      readonly amount: Money;
      readonly autoCashOut: Multiplier | null;
    }
  /** PlaceBet answered BetAccepted. */
  | {
      readonly type: "betAccepted";
      readonly betId: string;
      readonly clientBetId: string;
      readonly roundId: string | null;
      readonly roundNumber: number | null;
      readonly panel: PanelNumber;
      readonly amount: Money;
      readonly currency: string;
      readonly autoCashOut: Multiplier | null;
      readonly status: "placed" | "queued";
      readonly balance: Money;
    }
  /** BetActivated: a queued bet entered the betting phase. */
  | {
      readonly type: "betActivated";
      readonly betId: string;
      readonly roundId: string;
      readonly roundNumber: number;
    }
  /** The player asked to cancel the bet of a panel (CancelBet sent). */
  | { readonly type: "cancelRequested"; readonly panel: PanelNumber }
  /** CancelBet answered BetCancelled. */
  | {
      readonly type: "betCancelled";
      readonly betId: string;
      readonly refunded: Money;
      readonly refundStatus: "refunded" | "pending";
      readonly balance: Money | null;
    }
  /** The player asked to cash out the bet of a panel (CashOut sent). */
  | { readonly type: "cashOutRequested"; readonly panel: PanelNumber }
  /** CashOut answered CashOutResult.pending. */
  | { readonly type: "cashOutPending"; readonly betId: string }
  /** CashOut answered CashOutResult.cashedOut, or the server sent CashedOut (automatic or decided pending). */
  | {
      readonly type: "cashedOut";
      readonly betId: string;
      readonly multiplier: Multiplier;
      readonly payout: Money;
      readonly creditStatus: "credited" | "pending";
      readonly balance: Money | null;
      readonly auto: boolean;
    }
  /** CashOutRejected: late decision of a pending cash-out (the bet is lost). */
  | { readonly type: "cashOutRejected"; readonly betId: string; readonly code: string }
  /** BetSettled: the round crashed and the bet was not cashed out. */
  | {
      readonly type: "betSettled";
      readonly betId: string;
      readonly roundId: string;
      readonly status: "lost";
    }
  /** BetRefunded: the round was annulled. */
  | {
      readonly type: "betRefunded";
      readonly betId: string;
      readonly roundId: string;
      readonly refunded: Money;
      readonly refundStatus: "refunded" | "pending";
      readonly balance: Money | null;
    }
  /** BalanceUpdated: a pending credit or refund was confirmed. */
  | { readonly type: "balanceUpdated"; readonly balance: Money; readonly currency: string }
  /** A request of the panel failed with a server error code. */
  | { readonly type: "requestFailed"; readonly panel: PanelNumber; readonly code: BetErrorCode }
  /** The player dismissed the error of the panel (or changed the draft). */
  | { readonly type: "errorDismissed"; readonly panel: PanelNumber }
  /** Round phases the panels follow (from the round store). */
  | { readonly type: "roundBetting"; readonly roundId: string }
  | { readonly type: "roundFlying"; readonly roundId: string }
  | { readonly type: "roundCrashed"; readonly roundId: string }
  | { readonly type: "roundAnnulled"; readonly roundId: string };
