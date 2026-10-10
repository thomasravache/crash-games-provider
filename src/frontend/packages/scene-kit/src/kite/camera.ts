// Camera and wind of SPEC-0011 §6 (pure): altitude = log2(m) screens, at most 12 screens; wind intensity
// 0.2 + 0.8 · min(1, log10(m) / 2).

const maxAltitudeScreens = 12;

/** Altitude of the camera, in screens, at the multiplier. */
export function cameraAltitude(multiplier: number): number {
  if (!(multiplier > 1)) return 0;
  return Math.min(maxAltitudeScreens, Math.log2(multiplier));
}

/** Wind intensity, 0.2..1, at the multiplier. */
export function windIntensity(multiplier: number): number {
  const level = multiplier > 1 ? Math.log10(multiplier) / 2 : 0;
  return 0.2 + 0.8 * Math.min(1, level);
}
