// Scene part of the test hook (SPEC-0011 §6, test builds only): `window.__APOGEU_TEST__.scene`.

import type { EffectEntry } from "./effects";

/** State the scene is positioned in by `freeze` (phases of the RoundState of SPEC-0010@1). */
export interface SceneFreezeState {
  readonly phase: "betting" | "flying" | "crashed";
  /** Multiplier shown (the crash point when crashed). */
  readonly multiplier: number;
}

export interface SceneTestHook {
  /** Stops the scene clock and positions the scene `atMs` after the start of the phase of `state`. */
  freeze(atMs: number, state: SceneFreezeState): void | Promise<void>;
  /** Effects the scene started since it was mounted. */
  effectsLog(): readonly EffectEntry[];
  /** Duration of each rendered frame since the scene was mounted, ms. */
  frameTimes(): readonly number[];
}
