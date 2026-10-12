// Bets state mirrored from the server (SPEC-0026 §6, ADR-0004): the two bet panels, the wallet balance and the bet
// limits. Separate from the round store; the server is the source of truth (no optimistic balance). Money and
// multipliers stay decimal strings (ADR-0008): they are formatted, never computed with as Number.

import type { Money, Multiplier } from "@apogeu/contracts";

import type { BetErrorCode } from "./bet-errors";

/** Panel of the double bet: "APOSTA 1" and "APOSTA 2" (PlaceBet `panel`, SPEC-0022@3). */
export type PanelNumber = 1 | 2;

/**
 * State of one panel (SPEC-0026 §6):
 * - idle: no bet ("APOSTAR R$ x");
 * - queued: bet placed during the flight for the next round ("PRÓXIMA · CANCELAR");
 * - placed: bet in the betting phase of the current round ("CANCELAR");
 * - active: the round is flying ("RETIRAR R$ valor×m");
 * - cashing_out: the server answered CashOutPending ("Retirando…");
 * - cashed_out: CashedOut (won R$ y; the credit may still be pending);
 * - lost: BetSettled lost, or CashOutRejected;
 * - refunded: BetRefunded of an annulled round.
 */
export type PanelStatus =
  "idle" | "queued" | "placed" | "active" | "cashing_out" | "cashed_out" | "lost" | "refunded";

/** Request of the player still waiting for the server answer (the panel disables its button meanwhile). */
export type PanelRequest = "none" | "placing" | "cancelling" | "cashing_out";

export interface PanelState {
  readonly panel: PanelNumber;
  readonly status: PanelStatus;
  readonly request: PanelRequest;
  /** Bet id of the server (BetAccepted.betId); null while idle or before the acceptance. */
  readonly betId: string | null;
  /** Idempotency key sent with PlaceBet (uuid); null while idle. */
  readonly clientBetId: string | null;
  /** Round of the bet; null while queued (SPEC-0022@3). */
  readonly roundId: string | null;
  readonly amount: Money | null;
  readonly autoCashOut: Multiplier | null;
  /** Cash-out multiplier (cashed_out). */
  readonly multiplier: Multiplier | null;
  /** Cash-out payout (cashed_out). */
  readonly payout: Money | null;
  /** Whether the cash-out was the automatic one (CashedOut.auto). */
  readonly auto: boolean;
  /** "pending" while the credit of a cash-out or a refund is not confirmed (BalanceUpdated confirms it later). */
  readonly creditStatus: "credited" | "pending" | null;
  /** Refunded amount (refunded). */
  readonly refunded: Money | null;
  /** Last server error code of this panel (shown as the pt-BR message of bet-errors); null when none. */
  readonly error: BetErrorCode | null;
}

/** Bet limits of the operator and currency (GetBetLimits, SPEC-0020@2 / SPEC-0022@3). */
export interface BetLimits {
  readonly currency: string;
  readonly minBet: Money;
  readonly maxBet: Money;
  readonly maxWin: Money;
}

/** BetsState of SPEC-0026 §6. */
export interface BetsState {
  readonly panels: readonly [PanelState, PanelState];
  /** Balance confirmed by the server; null until the first wallet read. */
  readonly balance: Money | null;
  /** Currency of the balance (ISO 4217); null until the first wallet read. */
  readonly currency: string | null;
  /** Bet limits; null until GetBetLimits answers. */
  readonly limits: BetLimits | null;
}

/** Read side of the bets store (what the HUD and the test hook use). */
export interface BetsStateSource {
  getState(): BetsState;
  /** useSyncExternalStore-compatible subscription; returns the unsubscribe function. */
  subscribe(listener: () => void): () => void;
}

/** An open bet read back from the server on reconnection (GET /api/v1/me/bets, SPEC-0025@1). */
export interface OpenBet {
  readonly betId: string;
  readonly roundId: string | null;
  readonly panel: PanelNumber;
  readonly amount: Money;
  readonly autoCashOut: Multiplier | null;
  readonly status: "queued" | "placed" | "active";
}
