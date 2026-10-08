// Arch fixture (SPEC-0003 IT-03): allowed control. scene-kit reads the round state exposed by game-sdk (ADR-0004).
import type { RoundState } from "../../game-sdk/src/index";

export function readMultiplier(state: RoundState): number {
  return state.multiplier;
}
