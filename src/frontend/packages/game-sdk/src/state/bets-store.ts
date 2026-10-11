// Bets store without a framework (SPEC-0026 §4), like the round store: subscribable, consumed by React through
// useSyncExternalStore. It only mirrors what the server confirmed: the balance changes only with server values.

import { notImplemented } from "../not-implemented";
import type { BetsEvent } from "./bets-events";
import type { BetsState, BetsStateSource, PanelNumber, PanelState } from "./bets-state";

export interface BetsStore extends BetsStateSource {
  /** Reduces one event; stale, duplicated or unknown-bet events leave the state unchanged. */
  dispatch(event: BetsEvent): void;
}

/** Idle panel (no bet, no error). */
export function idlePanel(panel: PanelNumber): PanelState {
  return {
    panel,
    status: "idle",
    request: "none",
    betId: null,
    clientBetId: null,
    roundId: null,
    amount: null,
    autoCashOut: null,
    multiplier: null,
    payout: null,
    auto: false,
    creditStatus: null,
    refunded: null,
    error: null,
  };
}

export const initialBetsState: BetsState = {
  panels: [idlePanel(1), idlePanel(2)],
  balance: null,
  currency: null,
  limits: null,
};

/** Next state for one event; returns the same object when the event must be ignored. */
export function reduceBetsEvent(state: BetsState, event: BetsEvent): BetsState {
  return notImplemented(state, event);
}

/** Panel of a state by number. */
export function panelOf(state: BetsState, panel: PanelNumber): PanelState {
  return notImplemented(state, panel);
}

/** Store with the initial state, or `initial` (tests). */
export function createBetsStore(initial?: BetsState): BetsStore {
  return notImplemented(initial);
}
