// Test hook of SPEC-0010 §6 (ADR-0009): `window.__APOGEU_TEST__ = { state(): RoundState }`, installed only in test
// builds, so the E2E tests read the round state without depending on pixels of the scene.

import type { RoundState, RoundStateSource } from "@apogeu/game-sdk/state";
import { NotImplementedError } from "@apogeu/game-sdk";

export interface ApogeuTestHook {
  state(): RoundState;
}

declare global {
  interface Window {
    __APOGEU_TEST__?: ApogeuTestHook;
  }
}

/** Installs the hook when the build is a test build; a no-op otherwise. */
export function installTestHook(source: RoundStateSource): void {
  throw new NotImplementedError("installTestHook", source);
}
