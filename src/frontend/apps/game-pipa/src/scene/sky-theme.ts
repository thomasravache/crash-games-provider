// Sky palettes of the Pipa theme (SPEC-0010 §2, SPEC-0011 §6): day, sunset and night, from the Stitch design system
// "Pipa Crash" (sunset gradient gold → solar orange → tropical magenta → twilight violet, base canvas #080b16).

import type { SkyTheme } from "@apogeu/scene-kit";
import type { SkyGradients } from "@apogeu/scene-kit/pixi";

export const pipaSkyTheme: SkyTheme = {
  day: { top: "#38bdf8", bottom: "#bae6fd" },
  sunset: { top: "#7c3aed", bottom: "#f97316" },
  night: { top: "#080b16", bottom: "#312e81" },
};

/** Colour stops of the drawn skies, top → horizon: the theme's ends with the design's sunset ramp in between. */
export const pipaSkyGradients: SkyGradients = {
  day: ["#38bdf8", "#7dd3fc", "#bae6fd"],
  sunset: ["#7c3aed", "#ec4899", "#f97316", "#facc15"],
  night: ["#080b16", "#1e1b4b", "#312e81"],
};
