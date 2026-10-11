import type { BetsState, BetsStateSource } from "@apogeu/game-sdk/state";
import { useSyncExternalStore } from "react";

/** Bets state for React, through useSyncExternalStore (SPEC-0026 §4). */
export function useBetsState(source: BetsStateSource): BetsState {
  return useSyncExternalStore(
    (listener) => source.subscribe(listener),
    () => source.getState(),
    () => source.getState(),
  );
}
