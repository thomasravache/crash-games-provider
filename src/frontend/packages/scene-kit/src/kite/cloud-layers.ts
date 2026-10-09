// CloudLayers of SPEC-0011 §2: three cloud layers in parallax, each faster than the one behind it, following the
// camera; no parallax with the reduced MotionProfile.

import type { MotionProfile } from "./motion-profile";
import { notImplemented } from "./not-implemented";

export interface CloudLayerState {
  /** Parallax factor of the layer relative to the camera (0 = no parallax). */
  readonly parallax: number;
  /** Vertical offset of the layer, px. */
  readonly offsetY: number;
}

/** The three layers at the camera altitude (screens) and scene time. */
export function cloudLayers(
  altitude: number,
  timeMs: number,
  profile: MotionProfile,
): readonly CloudLayerState[] {
  return notImplemented(altitude, timeMs, profile);
}
