// Sky by multiplier (SPEC-0010 §2): day at 1x, sunset around 10x, night from 100x, with the theme's palettes.

import { NotImplementedError } from "./not-implemented";

export interface SkyPalette {
  /** Top of the sky gradient, `#rrggbb`. */
  readonly top: string;
  /** Horizon of the sky gradient, `#rrggbb`. */
  readonly bottom: string;
}

export interface SkyTheme {
  readonly day: SkyPalette;
  readonly sunset: SkyPalette;
  readonly night: SkyPalette;
}

/** Palette of the sky at the multiplier: theme.day at 1, theme.sunset at 10, theme.night at 100 and above. */
export function skyForMultiplier(multiplier: number, theme: SkyTheme): SkyPalette {
  throw new NotImplementedError("skyForMultiplier", multiplier, theme);
}
