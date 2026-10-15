// "Minhas apostas" (SPEC-0031, SPEC-0025@4): the player's own bets from GET /api/v1/me/bets, newest first, paged by an
// opaque cursor ("carregar mais"). Pages are appended without duplicating a bet; with no next cursor there is no
// "carregar mais".

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

/** `items` without the bets already in `known` (nor repeated inside the page), in their order. */
function newBets(known: readonly MyBetEntry[], items: readonly MyBetEntry[]): MyBetEntry[] {
  const seen = new Set(known.map((item) => item.betId));
  const fresh: MyBetEntry[] = [];
  for (const item of items) {
    if (seen.has(item.betId)) continue;
    seen.add(item.betId);
    fresh.push(item);
  }
  return fresh;
}

/** Next history state for one event. */
export function reduceMyBetsEvent(state: MyBetsState, event: MyBetsEvent): MyBetsState {
  switch (event.type) {
    case "myBetsRequested":
      return state.loading && state.error === null
        ? state
        : { ...state, loading: true, error: null };
    case "myBetsPage":
      return {
        items: event.append
          ? [...state.items, ...newBets(state.items, event.items)]
          : newBets([], event.items),
        nextCursor: event.nextCursor,
        loading: false,
        loaded: true,
        error: null,
      };
    case "myBetsFailed":
      return { ...state, loading: false, error: event.code };
  }
}

export function createMyBetsStore(initial: MyBetsState = initialMyBetsState): MyBetsStore {
  let state = initial;
  const listeners = new Set<() => void>();
  return {
    getState: () => state,
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    dispatch(event) {
      const next = reduceMyBetsEvent(state, event);
      if (next === state) return;
      state = next;
      for (const listener of [...listeners]) listener();
    },
  };
}
