// Sound preference (SPEC-0018 §6): kept only in the browser, localStorage "apogeu.sound" = {"muted":bool,"volume":n};
// a storage that throws or holds an invalid value behaves as the default, silently.

import { notImplemented } from "./not-implemented";

export interface SoundPreference {
  readonly muted: boolean;
  /** Master volume in [0, 1]. */
  readonly volume: number;
}

export const soundPreferenceKey = "apogeu.sound";

export const defaultSoundPreference: SoundPreference = { muted: false, volume: 0.7 };

/** The part of the Web Storage API the preference uses (window.localStorage in the browser). */
export interface PreferenceStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

export function loadSoundPreference(storage: PreferenceStorage | null): SoundPreference {
  return notImplemented(storage);
}

export function saveSoundPreference(
  storage: PreferenceStorage | null,
  preference: SoundPreference,
): void {
  return notImplemented(storage, preference);
}
