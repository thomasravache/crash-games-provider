// Sound preference (SPEC-0018 §6): kept only in the browser, localStorage "apogeu.sound" = {"muted":bool,"volume":n};
// a storage that throws or holds an invalid value behaves as the default, silently.

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

/** A volume in [0, 1]; anything else (NaN, ±Infinity) is null. */
export function normalizeVolume(volume: number): number | null {
  return Number.isFinite(volume) ? Math.min(1, Math.max(0, volume)) : null;
}

function parse(raw: string): SoundPreference | null {
  const value: unknown = JSON.parse(raw);
  if (typeof value !== "object" || value === null) return null;
  const { muted, volume } = value as { muted?: unknown; volume?: unknown };
  if (typeof muted !== "boolean" || typeof volume !== "number") return null;
  const normalized = normalizeVolume(volume);
  return normalized === null ? null : { muted, volume: normalized };
}

export function loadSoundPreference(storage: PreferenceStorage | null): SoundPreference {
  if (storage === null) return defaultSoundPreference;
  try {
    const raw = storage.getItem(soundPreferenceKey);
    return (raw === null ? null : parse(raw)) ?? defaultSoundPreference;
  } catch {
    // Blocked storage or invalid JSON: the default, silently (SPEC-0018 §5).
    return defaultSoundPreference;
  }
}

export function saveSoundPreference(
  storage: PreferenceStorage | null,
  preference: SoundPreference,
): void {
  if (storage === null) return;
  try {
    storage.setItem(
      soundPreferenceKey,
      JSON.stringify({ muted: preference.muted, volume: preference.volume }),
    );
  } catch {
    // Private mode or quota: the preference lasts only for this page, silently (SPEC-0018 §5).
  }
}
