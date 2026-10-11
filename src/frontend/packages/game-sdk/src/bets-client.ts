// Bets client (SPEC-0026 §6): connects to /hubs/bets with the session token, reads the limits and the wallet balance,
// sends the panel intents (PlaceBet, CancelBet, CashOut, demo recharge) and maps every server message to a bets store
// event. The store only shows what the server confirmed (no optimistic balance). After a reconnection the balance and
// the open bets are read again from the server (UT-03); GET /api/v1/me/bets answering 404 keeps the local panels.

import type { Money, Multiplier } from "@apogeu/contracts";

import { notImplemented } from "./not-implemented";
import type { BetErrorCode } from "./state/bet-errors";
import type { PanelNumber } from "./state/bets-state";
import type { BetsStore } from "./state/bets-store";
import type { RoundStateSource } from "./state/round-state";
import type { ConnectBetsHub } from "./transport/bets-hub-connection";
import type { FetchLike } from "./transport/session-api";

export interface BetsClientOptions {
  /** Base URL of the API (Host), without a trailing slash; the hub is `${apiBaseUrl}/hubs/bets`. */
  readonly apiBaseUrl: string;
  /** Round state the panels follow (placed → active on the flight, finished panels back to idle on the next betting). */
  readonly rounds: RoundStateSource;
  /** HTTP client; defaults to the global fetch. */
  readonly fetch?: FetchLike;
  /** Hub connection factory; defaults to createSignalRBetsConnection. */
  readonly connectHub?: ConnectBetsHub;
  /** clientBetId (uuid) generator; defaults to crypto.randomUUID. */
  readonly newClientBetId?: () => string;
}

/** What the player asks for when betting on a panel; values travel as decimal strings. */
export interface PlaceBetIntent {
  readonly amount: Money;
  /** Automatic cash-out target (≥ 1.01), or null without it. */
  readonly autoCashOut: Multiplier | null;
}

/** A bet request failed: the server code (or NETWORK/UNKNOWN); the same code is recorded in the panel state. */
export class BetError extends Error {
  readonly code: BetErrorCode;

  constructor(code: BetErrorCode) {
    super(code);
    this.name = "BetError";
    this.code = code;
  }
}

export interface BetsClient {
  readonly store: BetsStore;
  /**
   * Connects to /hubs/bets with the session token (memory only), subscribes to the server messages, reads the limits
   * (GetBetLimits) and the balance (GET /api/v1/wallet/balance) and starts following the round state.
   */
  start(sessionToken: string): Promise<void>;
  /** PlaceBet on the panel; rejects with BetError (the panel shows the message). */
  placeBet(panel: PanelNumber, intent: PlaceBetIntent): Promise<void>;
  /** CancelBet of the panel's placed or queued bet; rejects with BetError. */
  cancelBet(panel: PanelNumber): Promise<void>;
  /** CashOut of the panel's active bet (CashedOut or CashOutPending); rejects with BetError. */
  cashOut(panel: PanelNumber): Promise<void>;
  /** POST /api/v1/wallet/demo/recharge and the new balance; rejects with BetError. */
  rechargeDemo(): Promise<void>;
  /** Reconnection reload: balance from the wallet, open bets from GET /api/v1/me/bets (404 keeps the panels). */
  reload(): Promise<void>;
  stop(): Promise<void>;
}

export function createBetsClient(options: BetsClientOptions): BetsClient {
  return notImplemented(options);
}
