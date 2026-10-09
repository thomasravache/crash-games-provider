// Adapter between the round state and the scene (ADR-0004): subscribes to the state exposed by game-sdk and calls
// only GameAnimator — prepare on betting, takeoff on flying, setMultiplier while flying, crash on crashed, reset on
// annulled. Imports `@apogeu/game-sdk/state`, never the transport.

import type { RoundState, RoundStateSource } from "@apogeu/game-sdk/state";

import type { GameAnimator } from "./game-animator";

/** Drives the animator from the state; returns the function that stops it. */
export function bindGameAnimator(source: RoundStateSource, animator: GameAnimator): () => void {
  let shown: { roundId: string | null; phase: RoundState["phase"]; multiplier: number } = {
    roundId: null,
    phase: "idle",
    multiplier: 1,
  };

  const apply = (state: RoundState): void => {
    const changedRound = state.roundId !== shown.roundId || state.phase !== shown.phase;
    if (changedRound) {
      switch (state.phase) {
        case "betting":
          animator.prepare();
          break;
        case "flying": {
          const startedAt =
            state.startedAt === null
              ? Date.now()
              : Date.parse(state.startedAt) - state.serverOffsetMs;
          animator.takeoff(startedAt);
          animator.setMultiplier(state.multiplier);
          break;
        }
        case "crashed":
          animator.crash(state.crashPoint === null ? state.multiplier : Number(state.crashPoint));
          break;
        case "annulled":
          animator.reset();
          break;
        case "idle":
          if (shown.phase !== "idle") animator.reset();
          break;
      }
    } else if (state.phase === "flying" && state.multiplier !== shown.multiplier) {
      animator.setMultiplier(state.multiplier);
    }
    shown = { roundId: state.roundId, phase: state.phase, multiplier: state.multiplier };
  };

  apply(source.getState());
  const unsubscribe = source.subscribe(() => {
    apply(source.getState());
  });
  return unsubscribe;
}
