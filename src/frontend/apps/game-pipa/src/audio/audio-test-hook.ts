// Audio part of the test hook (SPEC-0018 §6, ADR-0009): `window.__APOGEU_TEST__.audio` — cueLog() and state() —
// installed next to the SPEC-0010 hook, only in test builds.

import type { AudioTestHook } from "@apogeu/scene-kit";

import { isTestBuild } from "../test-hook";

declare module "../test-hook" {
  interface ApogeuTestHook {
    audio?: AudioTestHook;
  }
}

/** Installs the audio hook in test builds (a no-op otherwise). */
export function installAudioTestHook(audio: AudioTestHook): void {
  const hook = window.__APOGEU_TEST__;
  if (!isTestBuild || hook === undefined) return;
  hook.audio = { cueLog: () => audio.cueLog(), state: () => audio.state() };
}
