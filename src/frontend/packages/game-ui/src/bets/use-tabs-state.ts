import type {
  LiveBetsState,
  LiveBetsStateSource,
  MyBetsState,
  MyBetsStateSource,
} from "@apogeu/game-sdk/state";
import { useSyncExternalStore } from "react";

/** "Todas as apostas" state for React, through useSyncExternalStore (SPEC-0031). */
export function useLiveBetsState(source: LiveBetsStateSource): LiveBetsState {
  return useSyncExternalStore(
    (listener) => source.subscribe(listener),
    () => source.getState(),
    () => source.getState(),
  );
}

/** "Minhas apostas" state for React, through useSyncExternalStore (SPEC-0031). */
export function useMyBetsState(source: MyBetsStateSource): MyBetsState {
  return useSyncExternalStore(
    (listener) => source.subscribe(listener),
    () => source.getState(),
    () => source.getState(),
  );
}
