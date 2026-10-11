// Sound of the Pipa game (SPEC-0018): the PipaAudio of scene-kit driven by the round state through the same adapter as
// the scene (GameAudio has the GameAnimator calls, ADR-0004). Loaded on demand, so the sound engine stays out of the
// initial bundle (ADR-0019). Imports only the state of game-sdk, never the transport.

import type { RoundStateSource } from "@apogeu/game-sdk/state";
import { bindGameAnimator, createPipaAudio, type PipaAudio } from "@apogeu/scene-kit";

import { installAudioTestHook } from "./audio-test-hook";

/** Creates the game's sound (silent until unlock()) and binds it to the round state. */
export function mountPipaAudio(source: RoundStateSource): PipaAudio {
  const audio = createPipaAudio();
  bindGameAnimator(source, audio);
  installAudioTestHook(audio);
  return audio;
}
