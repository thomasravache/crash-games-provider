// Sky by multiplier (SPEC-0010 §2): day at 1x, sunset around 10x, night from 100x, with the theme's palettes. Between
// them the colours blend on a logarithmic scale, so the sky changes as fast as the multiplier "feels".

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

function channels(hex: string): [number, number, number] {
  const value = Number.parseInt(hex.slice(1), 16);
  return [(value >> 16) & 0xff, (value >> 8) & 0xff, value & 0xff];
}

/** Colour between `from` (t = 0) and `to` (t = 1). */
export function mixColor(from: string, to: string, t: number): string {
  const a = channels(from);
  const b = channels(to);
  return `#${a
    .map((channel, index) => Math.round(channel + ((b[index] ?? channel) - channel) * t))
    .map((channel) => channel.toString(16).padStart(2, "0"))
    .join("")}`;
}

function mix(from: SkyPalette, to: SkyPalette, t: number): SkyPalette {
  if (t <= 0) return from;
  if (t >= 1) return to;
  return { top: mixColor(from.top, to.top, t), bottom: mixColor(from.bottom, to.bottom, t) };
}

/** Palette of the sky at the multiplier: theme.day at 1, theme.sunset at 10, theme.night at 100 and above. */
export function skyForMultiplier(multiplier: number, theme: SkyTheme): SkyPalette {
  const level = multiplier > 1 ? Math.log10(multiplier) : 0;
  return level <= 1
    ? mix(theme.day, theme.sunset, level)
    : mix(theme.sunset, theme.night, level - 1);
}
