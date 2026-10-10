// The game's sound seen from the UI (SPEC-0018 §6): loads the sound engine on demand, unlocks it on the player's
// gestures (autoplay policy: no sound before the first one) and exposes the preference to the "Som" control.
// Created once per page, outside React (StrictMode would mount the engine twice).

import type { RoundStateSource } from "@apogeu/game-sdk/state";
import type { PipaAudio, SoundPreference } from "@apogeu/scene-kit";

/** Functions, not methods: React's useSyncExternalStore takes subscribe and getSnapshot on their own. */
export interface SoundController {
  readonly subscribe: (listener: () => void) => () => void;
  /** The preference shown by the sound control; null until the sound engine is loaded. */
  readonly getSnapshot: () => SoundPreference | null;
  readonly setMuted: (muted: boolean) => void;
  readonly setVolume: (volume: number) => void;
}

/**
 * Events that count as the player's gesture on any game control (the ones browsers take as user activation). Kept for
 * the whole page: a later gesture resumes a context the browser did not let start.
 */
const gestureEvents = ["pointerdown", "pointerup", "keydown", "touchend"] as const;

export function createSoundController(
  source: RoundStateSource,
  target: EventTarget = document,
): SoundController {
  let audio: PipaAudio | null = null;
  let gestured = false;
  let snapshot: SoundPreference | null = null;
  const listeners = new Set<() => void>();

  const publish = (): void => {
    snapshot = audio?.preference() ?? null;
    for (const listener of listeners) listener();
  };

  const onGesture = (): void => {
    gestured = true;
    void audio?.unlock();
  };
  for (const type of gestureEvents) target.addEventListener(type, onGesture, { capture: true });

  import("./mount-audio")
    .then(({ mountPipaAudio }) => {
      audio = mountPipaAudio(source);
      if (gestured) void audio.unlock();
      publish();
    })
    // Without the sound module the game goes on silent; the HUD carries every state (ADR-0020).
    .catch(() => undefined);

  return {
    subscribe: (listener) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    getSnapshot: () => snapshot,
    setMuted: (muted) => {
      audio?.setMuted(muted);
      publish();
    },
    setVolume: (volume) => {
      audio?.setVolume(volume);
      publish();
    },
  };
}
