// Shake of the "PIPA CORTADA!" text on the crash (SPEC-0026 §2, UT-07, ADR-0021): at most 300 ms after RoundCrashed,
// and never with `prefers-reduced-motion: reduce`.

import type { RoundPhase } from "@apogeu/game-sdk/state";

import { notImplemented } from "./not-implemented";

/** Longest shake of the crash text. */
export const crashShakeDurationMs = 300;

/** Class applied to the crash text while it shakes (keyframes in hud-base.css). */
export const crashShakeClassName = "hud-crash-shake";

export const reducedMotionQuery = "(prefers-reduced-motion: reduce)";

/** The part of window the preference reads (window.matchMedia in the browser). */
export interface MotionMedia {
  matchMedia(query: string): { readonly matches: boolean };
}

/** Whether the player asked for less motion; false without matchMedia. */
export function prefersReducedMotion(media?: MotionMedia): boolean {
  return notImplemented(media);
}

/**
 * True from the moment the round `roundId` enters "crashed" for crashShakeDurationMs, once per round; always false
 * with reduced motion.
 */
export function useCrashShake(
  phase: RoundPhase,
  roundId: string | null,
  reducedMotion: boolean,
): boolean {
  return notImplemented(phase, roundId, reducedMotion);
}
