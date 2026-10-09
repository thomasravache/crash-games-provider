// Test hook of SPEC-0010 §6 (ADR-0009): `window.__APOGEU_TEST__ = { state(): RoundState }`, installed only in test
// builds, so the E2E tests read the round state without depending on pixels of the scene.

import type { RoundState, RoundStateSource } from "@apogeu/game-sdk/state";

export interface ApogeuTestHook {
  state(): RoundState;
}

declare global {
  interface Window {
    __APOGEU_TEST__?: ApogeuTestHook;
  }
}

/**
 * Test builds: the Vite dev server (how Aspire serves the game locally) or a build with VITE_APOGEU_TEST_HOOK=true.
 * A production build replaces both by constants, so the hook is dropped from its bundle.
 */
export const isTestBuild: boolean =
  import.meta.env.DEV || import.meta.env.VITE_APOGEU_TEST_HOOK === "true";

/** Installs the hook when the build is a test build; a no-op otherwise. */
export function installTestHook(source: RoundStateSource): void {
  if (!isTestBuild) return;
  window.__APOGEU_TEST__ = { state: () => source.getState() };
}
