import type { RoundState, RoundStateSource } from "@apogeu/game-sdk/state";
import { useEffect, useState, useSyncExternalStore } from "react";

/** Round state for React, through useSyncExternalStore (SPEC-0010 §4). */
export function useRoundState(source: RoundStateSource): RoundState {
  return useSyncExternalStore(
    (listener) => source.subscribe(listener),
    () => source.getState(),
    () => source.getState(),
  );
}

/** Local clock refreshed every `intervalMs` while `active` (countdowns); a fixed value otherwise. */
export function useNow(active: boolean, intervalMs = 250): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!active) return undefined;
    const update = (): void => {
      setNow(Date.now());
    };
    // First refresh right away (the value may be stale since the last activation), then every interval.
    const first = setTimeout(update, 0);
    const timer = setInterval(update, intervalMs);
    return () => {
      clearTimeout(first);
      clearInterval(timer);
    };
  }, [active, intervalMs]);
  return now;
}
