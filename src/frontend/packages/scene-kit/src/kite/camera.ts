// Camera and wind of SPEC-0011 §6 (pure): altitude = log2(m) screens, at most 12 screens; wind intensity
// 0.2 + 0.8 · min(1, log10(m) / 2).

import { notImplemented } from "./not-implemented";

/** Altitude of the camera, in screens, at the multiplier. */
export function cameraAltitude(multiplier: number): number {
  return notImplemented(multiplier);
}

/** Wind intensity, 0.2..1, at the multiplier. */
export function windIntensity(multiplier: number): number {
  return notImplemented(multiplier);
}
