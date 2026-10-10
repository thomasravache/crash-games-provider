// PipaAudio (SPEC-0018 §4): the GameAudio of the Pipa, composed of AudioMix, CueScheduler and the synthesized Voices,
// behind an AudioGate that creates/resumes the AudioContext only in unlock().
//
// Graph contract (what the tests observe): every voice reaches the destination through one master GainNode, the only
// node connected directly to `destination`; its gain is volume · (muted ? 0 : 1) · 0.8, changed with 50 ms ramps.

import type { VisibilitySource } from "../kite/visibility";
import type { AudioTestHook, GameAudio } from "./game-audio";
import type { PreferenceStorage, SoundPreference } from "./sound-preference";
import type { AudioContextLike } from "./web-audio";
import { notImplemented } from "./not-implemented";

export interface PipaAudioOptions {
  /**
   * Creates the AudioContext; called at most once, and only by unlock(). Absent: `new AudioContext()` when the browser
   * has one. Absent without Web Audio, throwing or returning null: the game stays muted, without errors.
   */
  readonly createContext?: () => AudioContextLike | null;
  /** Where the preference "apogeu.sound" is kept. Absent: window.localStorage when reachable. null: not kept. */
  readonly storage?: PreferenceStorage | null;
  /** Hidden tab: the context is suspended while hidden. Absent: the document when there is one. null: not watched. */
  readonly visibility?: VisibilitySource | null;
}

export interface PipaAudio extends GameAudio, AudioTestHook {
  /** The current sound preference (loaded from the storage at creation; default {muted:false, volume:0.7}). */
  preference(): SoundPreference;
}

export function createPipaAudio(options: PipaAudioOptions = {}): PipaAudio {
  return notImplemented(options);
}
