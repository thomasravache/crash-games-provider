// AudioMix (SPEC-0018 §6): pure mappings of the multiplier to gain and pitch, and the master gain.

import { notImplemented } from "./not-implemented";

/** Wind gain of the betting phase (and of 1x). */
export const bettingWindGain = 0.15;
/** Ceiling of the master gain (ADR-0024). */
export const masterGainCeiling = 0.8;
/** Every master gain change is a ramp of this length, ms (no clicks). */
export const masterRampMs = 50;

/** windGain(m) = 0.15 + 0.45 · min(1, log10(m)/2), in [0.15, 0.60]. */
export function windGain(multiplier: number): number {
  return notImplemented(multiplier);
}

/** tensionHz(m) = 220 · 2^(min(log2(m), 6)/3), limited to [220, 880] Hz. */
export function tensionHz(multiplier: number): number {
  return notImplemented(multiplier);
}

/** milestoneHz(i) = 523.25 · 2^(2i/12): one whole tone higher per milestone (i = 0..5 for 2x … 100x). */
export function milestoneHz(index: number): number {
  return notImplemented(index);
}

/** Master gain = volume · (muted ? 0 : 1) · 0.8. */
export function masterGain(volume: number, muted: boolean): number {
  return notImplemented(volume, muted);
}
