// Store without a framework (SPEC-0010 §4): subscribable, consumed by React through useSyncExternalStore and by the
// scene through a direct subscription.

import { NotImplementedError } from "../not-implemented";
import type { RoundEvent } from "./events";
import type { ConnectionStatus, RoundState, RoundStateSource } from "./round-state";

export interface RoundStore extends RoundStateSource {
  /** Reduces one server event; stale, duplicated or out-of-order events leave the state unchanged. */
  dispatch(event: RoundEvent): void;
  setConnection(status: ConnectionStatus): void;
  setServerOffset(serverOffsetMs: number): void;
  /** Recomputes the interpolated multiplier at the local instant (called every frame while flying). */
  tick(localNowMs: number): void;
}

export const initialRoundState: RoundState = {
  phase: "idle",
  roundId: null,
  roundNumber: null,
  commitment: null,
  bettingEndsAt: null,
  startedAt: null,
  growthK: null,
  multiplier: 1,
  crashPoint: null,
  revealedSeed: null,
  history: [],
  serverOffsetMs: 0,
  connection: "connecting",
};

export function createRoundStore(): RoundStore {
  throw new NotImplementedError("createRoundStore");
}
