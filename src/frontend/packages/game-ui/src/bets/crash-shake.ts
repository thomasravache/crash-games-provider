// Shake of the "PIPA CORTADA!" text on the crash (SPEC-0026 §2, UT-07, ADR-0021): at most 300 ms after RoundCrashed,
// and never with `prefers-reduced-motion: reduce`.

import type { RoundPhase } from "@apogeu/game-sdk/state";
import { useEffect, useState } from "react";

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
  if (media === undefined || typeof media.matchMedia !== "function") return false;
  return media.matchMedia(reducedMotionQuery).matches;
}

/** The browser window as a MotionMedia, or undefined outside the browser. */
export function browserMotionMedia(): MotionMedia | undefined {
  return typeof window === "undefined" ? undefined : window;
}

/**
 * True from the moment the round `roundId` enters "crashed" for crashShakeDurationMs, once per round; always false
 * with reduced motion. True already on the first render of the crash (no effect delay).
 */
export function useCrashShake(
  phase: RoundPhase,
  roundId: string | null,
  reducedMotion: boolean,
): boolean {
  // The round whose shake already ended; { roundId } so a null round id is distinct from "none yet".
  const [ended, setEnded] = useState<{ readonly roundId: string | null } | null>(null);
  const shaking = phase === "crashed" && !reducedMotion && ended?.roundId !== roundId;
  useEffect(() => {
    if (!shaking) return undefined;
    const timer = setTimeout(() => {
      setEnded({ roundId });
    }, crashShakeDurationMs);
    return () => {
      clearTimeout(timer);
    };
  }, [shaking, roundId]);
  return shaking;
}
