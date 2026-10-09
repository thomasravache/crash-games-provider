import { pipaSkyTheme } from "@apogeu/game-pipa/scene/sky-theme";
import { skyForMultiplier } from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

// SPEC-0010 UT-05: the sky follows the multiplier — day at 1x, sunset at 10x, night at 100x — with the palettes of the
// Pipa theme.

describe("SPEC-0010 céu por multiplicador", () => {
  test("SPEC-0010:UT-05 as paletas de dia, pôr do sol e noite do tema Pipa são cores distintas", () => {
    const palettes = [pipaSkyTheme.day, pipaSkyTheme.sunset, pipaSkyTheme.night];

    for (const palette of palettes) {
      expect(palette.top).toMatch(/^#[0-9a-f]{6}$/i);
      expect(palette.bottom).toMatch(/^#[0-9a-f]{6}$/i);
    }
    expect(new Set(palettes.map((palette) => `${palette.top}/${palette.bottom}`)).size).toBe(3);
  });

  test.each([
    [1, "day"],
    [10, "sunset"],
    [100, "night"],
  ] as const)("SPEC-0010:UT-05 multiplicador %f → paleta %s do tema Pipa", (value, palette) => {
    expect(skyForMultiplier(value, pipaSkyTheme)).toEqual(pipaSkyTheme[palette]);
  });
});
