// CloudLayers of SPEC-0011 §2: three cloud layers in parallax, each faster than the one behind it, following the
// camera; no parallax with the reduced MotionProfile.

import type { MotionProfile } from "./motion-profile";

export interface CloudLayerState {
  /** Parallax factor of the layer relative to the camera (0 = no parallax). */
  readonly parallax: number;
  /** Vertical offset of the layer, px. */
  readonly offsetY: number;
}

/** Parallax of the far, middle and near layers. */
const parallaxFactors: readonly number[] = [0.35, 0.65, 1.1];

/** Screen height the offsets are computed for when the renderer does not give one, px. */
const referenceScreenHeight = 844;

/** The three layers at the camera altitude (screens) and scene time. */
export function cloudLayers(
  altitude: number,
  timeMs: number,
  profile: MotionProfile,
  screenHeight = referenceScreenHeight,
): readonly CloudLayerState[] {
  return parallaxFactors.map((factor, index) => {
    if (profile === "reduced") return { parallax: 0, offsetY: 0 };
    // A slow vertical breathing keeps the far layers alive while the camera stands still.
    const drift = Math.sin(timeMs / (9_000 + 2_000 * index)) * 4;
    return { parallax: factor, offsetY: altitude * screenHeight * factor + drift };
  });
}
