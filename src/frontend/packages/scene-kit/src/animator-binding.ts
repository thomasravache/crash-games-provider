// Adapter between the round state and the scene (ADR-0004): subscribes to the state exposed by game-sdk and calls
// only GameAnimator — prepare on betting, takeoff on flying, setMultiplier while flying, crash on crashed, reset on
// annulled. Imports `@apogeu/game-sdk/state`, never the transport.

import type { RoundStateSource } from "@apogeu/game-sdk/state";

import type { GameAnimator } from "./game-animator";
import { NotImplementedError } from "./not-implemented";

/** Drives the animator from the state; returns the function that stops it. */
export function bindGameAnimator(source: RoundStateSource, animator: GameAnimator): () => void {
  throw new NotImplementedError("bindGameAnimator", animator, source);
}
