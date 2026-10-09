// Sky of SPEC-0011 §6 (pure): palette by multiplier — day [1, 5) · sunset [5, 25) · night ≥ 25, blended continuously
// between the bands — plus the stars (opacity 0 below 10x → 1 from 25x) and the moon (visible from 50x).

import { mixColor, type SkyPalette, type SkyTheme } from "../sky";

export type SkyBand = "day" | "sunset" | "night";

export interface SkyState {
  /** Band the multiplier is in. */
  readonly band: SkyBand;
  /** Continuous position on the ramp: 0 = day, 1 = sunset, 2 = night (never decreases with the multiplier). */
  readonly progress: number;
  /** Colours of the sky gradient at this multiplier. */
  readonly palette: SkyPalette;
}

/** Start of each band of the ramp. */
const sunsetAt = 5;
const nightAt = 25;
const starsFrom = 10;
const moonFrom = 50;

/** Position on the ramp: log scale inside each band, so the sky changes as fast as the multiplier "feels". */
function rampProgress(multiplier: number): number {
  if (!(multiplier > 1)) return 0;
  if (multiplier < sunsetAt) return Math.log(multiplier) / Math.log(sunsetAt);
  if (multiplier < nightAt)
    return 1 + Math.log(multiplier / sunsetAt) / Math.log(nightAt / sunsetAt);
  return 2;
}

function mixPalette(from: SkyPalette, to: SkyPalette, t: number): SkyPalette {
  if (t <= 0) return from;
  if (t >= 1) return to;
  return { top: mixColor(from.top, to.top, t), bottom: mixColor(from.bottom, to.bottom, t) };
}

export function skyAt(multiplier: number, theme: SkyTheme): SkyState {
  const progress = rampProgress(multiplier);
  const band: SkyBand = progress < 1 ? "day" : progress < 2 ? "sunset" : "night";
  const palette =
    progress < 1
      ? mixPalette(theme.day, theme.sunset, progress)
      : mixPalette(theme.sunset, theme.night, progress - 1);
  return { band, progress, palette };
}

/** Opacity of the stars, 0..1. */
export function starsOpacity(multiplier: number): number {
  if (!(multiplier >= starsFrom)) return 0;
  if (multiplier >= nightAt) return 1;
  return Math.log(multiplier / starsFrom) / Math.log(nightAt / starsFrom);
}

export function moonVisible(multiplier: number): boolean {
  return multiplier >= moonFrom;
}

/** Opacity of the moon: rises from a faint disc at 50x to full at about 80x (0 below 50x). */
export function moonOpacity(multiplier: number): number {
  if (!moonVisible(multiplier)) return 0;
  return Math.min(1, 0.35 + Math.log(multiplier / moonFrom) / Math.log(1.6));
}
