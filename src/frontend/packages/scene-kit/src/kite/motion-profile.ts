// MotionProfile of SPEC-0011 §4 (ADR-0020, ADR-0021): `reduced` when the player asks the system for less motion
// (`prefers-reduced-motion: reduce`), `full` otherwise. Passed to every subsystem of the scene.

import { notImplemented } from "./not-implemented";

export type MotionProfile = "full" | "reduced";

/** The part of `window` the profile is read from (absent outside the browser). */
export interface MotionMediaSource {
  matchMedia(query: string): { readonly matches: boolean };
}

/** `reduced` when `(prefers-reduced-motion: reduce)` matches; `full` otherwise or without `matchMedia`. */
export function motionProfileFrom(media: MotionMediaSource | undefined): MotionProfile {
  return notImplemented(media);
}
