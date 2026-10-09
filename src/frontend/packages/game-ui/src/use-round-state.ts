import type { RoundState, RoundStateSource } from "@apogeu/game-sdk/state";

import { NotImplementedError } from "./not-implemented";

/** Round state for React, through useSyncExternalStore (SPEC-0010 §4). */
export function useRoundState(source: RoundStateSource): RoundState {
  throw new NotImplementedError("useRoundState", source);
}
