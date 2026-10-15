// "Minhas apostas" (SPEC-0031, SPEC-0025@4): the player's own bets from GET /api/v1/me/bets, newest first, paged by an
// opaque cursor ("carregar mais"). Pages are appended without duplicating a bet; with no next cursor there is no
// "carregar mais".
//
// SCAFFOLD (SPEC-0031): signatures only; the reducer and the store are implemented in the green phase.

import type { Money, Multiplier } from "@apogeu/contracts";

export type MyBetStatus =
  "placed" | "queued" | "active" | "cashed_out" | "lost" | "cancelled" | "refunded";

/** An item of GET /api/v1/me/bets (MyBetItem of SPEC-0025@4). */
export interface MyBetEntry {
  readonly betId: string;
  readonly roundId: string | null;
  readonly roundNumber: number | null;
  readonly panel: number;
  readonly amount: Money;
  readonly currency: string;
  readonly autoCashOut: Multiplier | null;
  readonly multiplier: Multiplier | null;
  readonly payout: Money | null;
  readonly status: MyBetStatus;
  readonly placedAt: string;
}

export interface MyBetsState {
  readonly items: readonly MyBetEntry[];
  /** Cursor of the next page; null on the last page (or before the first read). */
  readonly nextCursor: string | null;
  /** A page request is in flight. */
  readonly loading: boolean;
  /** The first page was read. */
  readonly loaded: boolean;
  /** Code of the last failed read; null when none. */
  readonly error: string | null;
}

export type MyBetsEvent =
  | { readonly type: "myBetsRequested" }
  | {
      readonly type: "myBetsPage";
      /** false: the first page (replaces the list); true: the next page (appended). */
      readonly append: boolean;
      readonly items: readonly MyBetEntry[];
      readonly nextCursor: string | null;
    }
  | { readonly type: "myBetsFailed"; readonly code: string };

export interface MyBetsStateSource {
  getState(): MyBetsState;
  subscribe(listener: () => void): () => void;
}

export interface MyBetsStore extends MyBetsStateSource {
  dispatch(event: MyBetsEvent): void;
}

export const initialMyBetsState: MyBetsState = {
  items: [],
  nextCursor: null,
  loading: false,
  loaded: false,
  error: null,
};

/** Next history state for one event. */
export function reduceMyBetsEvent(_state: MyBetsState, event: MyBetsEvent): MyBetsState {
  throw new Error(`SPEC-0031: reduceMyBetsEvent(${event.type}) is not implemented yet`);
}

export function createMyBetsStore(): MyBetsStore {
  return {
    getState: () => initialMyBetsState,
    subscribe: () => () => undefined,
    dispatch(event) {
      reduceMyBetsEvent(initialMyBetsState, event);
    },
  };
}
