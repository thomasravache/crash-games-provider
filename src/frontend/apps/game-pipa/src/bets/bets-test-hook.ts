// Bets part of the test hook (SPEC-0026 §6, ADR-0009): `window.__APOGEU_TEST__.bets()` → BetsState, installed next to
// the SPEC-0010 hook, only in test builds.

import type { BetsState, BetsStateSource } from "@apogeu/game-sdk/state";

import { notImplemented } from "./not-implemented";

declare module "../test-hook" {
  interface ApogeuTestHook {
    bets?: () => BetsState;
  }
}

/** Installs `__APOGEU_TEST__.bets` in test builds (a no-op otherwise); call it after installTestHook. */
export function installBetsTestHook(source: BetsStateSource): void {
  notImplemented(source);
}
