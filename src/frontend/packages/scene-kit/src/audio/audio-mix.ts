// AudioMix (SPEC-0018 §6): pure mappings of the multiplier to gain and pitch, and the master gain.

/** Wind gain of the betting phase (and of 1x). */
export const bettingWindGain = 0.15;
/** Ceiling of the master gain (ADR-0024). */
export const masterGainCeiling = 0.8;
/** Every master gain change is a ramp of this length, ms (no clicks). */
export const masterRampMs = 50;

const clamp = (value: number, low: number, high: number): number =>
  Math.min(high, Math.max(low, value));

/** Below 1x (or not a number) the mix is the one of 1x. */
const atLeastOne = (multiplier: number): number => (multiplier > 1 ? multiplier : 1);

/** windGain(m) = 0.15 + 0.45 · min(1, log10(m)/2), in [0.15, 0.60]. */
export function windGain(multiplier: number): number {
  return bettingWindGain + 0.45 * Math.min(1, Math.log10(atLeastOne(multiplier)) / 2);
}

/** tensionHz(m) = 220 · 2^(min(log2(m), 6)/3), limited to [220, 880] Hz. */
export function tensionHz(multiplier: number): number {
  const octaves = Math.min(Math.log2(atLeastOne(multiplier)), 6) / 3;
  return clamp(220 * 2 ** octaves, 220, 880);
}

/** milestoneHz(i) = 523.25 · 2^(2i/12): one whole tone higher per milestone (i = 0..5 for 2x … 100x). */
export function milestoneHz(index: number): number {
  return 523.25 * 2 ** ((2 * index) / 12);
}

/** Master gain = volume · (muted ? 0 : 1) · 0.8. */
export function masterGain(volume: number, muted: boolean): number {
  if (muted || !(volume > 0)) return 0;
  return Math.min(1, volume) * masterGainCeiling;
}
