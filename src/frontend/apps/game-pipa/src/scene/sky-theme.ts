// Sky palettes of the Pipa theme (SPEC-0010 §2): day, sunset and night, from the Stitch design system "Pipa Crash"
// (sunset gradient gold → solar orange → tropical magenta → twilight violet, base canvas #080b16).

import type { SkyTheme } from "@apogeu/scene-kit";

export const pipaSkyTheme: SkyTheme = {
  day: { top: "#38bdf8", bottom: "#bae6fd" },
  sunset: { top: "#7c3aed", bottom: "#f97316" },
  night: { top: "#080b16", bottom: "#312e81" },
};
