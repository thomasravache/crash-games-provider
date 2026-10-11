import type { BetsState, BetsStateSource } from "@apogeu/game-sdk/state";

import { notImplemented } from "./not-implemented";

/** Bets state for React, through useSyncExternalStore (SPEC-0026 §4). */
export function useBetsState(source: BetsStateSource): BetsState {
  return notImplemented(source);
}
