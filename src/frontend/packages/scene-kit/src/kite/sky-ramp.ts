// Sky of SPEC-0011 §6 (pure): palette by multiplier — day [1, 5) · sunset [5, 25) · night ≥ 25, blended continuously
// between the bands — plus the stars (opacity 0 below 10x → 1 from 25x) and the moon (visible from 50x).

import type { SkyPalette, SkyTheme } from "../sky";
import { notImplemented } from "./not-implemented";

export type SkyBand = "day" | "sunset" | "night";

export interface SkyState {
  /** Band the multiplier is in. */
  readonly band: SkyBand;
  /** Continuous position on the ramp: 0 = day, 1 = sunset, 2 = night (never decreases with the multiplier). */
  readonly progress: number;
  /** Colours of the sky gradient at this multiplier. */
  readonly palette: SkyPalette;
}

export function skyAt(multiplier: number, theme: SkyTheme): SkyState {
  return notImplemented(multiplier, theme);
}

/** Opacity of the stars, 0..1. */
export function starsOpacity(multiplier: number): number {
  return notImplemented(multiplier);
}

export function moonVisible(multiplier: number): boolean {
  return notImplemented(multiplier);
}
