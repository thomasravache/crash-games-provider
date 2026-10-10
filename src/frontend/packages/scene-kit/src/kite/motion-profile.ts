// MotionProfile of SPEC-0011 §4 (ADR-0020, ADR-0021): `reduced` when the player asks the system for less motion
// (`prefers-reduced-motion: reduce`), `full` otherwise. Passed to every subsystem of the scene.

export type MotionProfile = "full" | "reduced";

/** The part of `window` the profile is read from (absent outside the browser). */
export interface MotionMediaSource {
  matchMedia(query: string): { readonly matches: boolean };
}

export const reducedMotionQuery = "(prefers-reduced-motion: reduce)";

/** `reduced` when `(prefers-reduced-motion: reduce)` matches; `full` otherwise or without `matchMedia`. */
export function motionProfileFrom(media: MotionMediaSource | undefined): MotionProfile {
  if (media === undefined || typeof media.matchMedia !== "function") return "full";
  return media.matchMedia(reducedMotionQuery).matches ? "reduced" : "full";
}
