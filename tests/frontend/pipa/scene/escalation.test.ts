import { pipaSkyTheme } from "@apogeu/game-pipa/scene/sky-theme";
import {
  cameraAltitude,
  moonVisible,
  skyAt,
  starsOpacity,
  windIntensity,
  type SkyBand,
} from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

// SPEC-0011 UT-03: escalation of tension (ADR-0021) — sky, stars, moon, camera and wind in the bands of the contract
// (§6) and never decreasing as the multiplier grows.

const checkpoints = [1, 5, 10, 25, 50, 100] as const;

/** 1x … 1000x in 0.5% steps, plus the checkpoints and the edges just below them. */
const grid: readonly number[] = (() => {
  const values = new Set<number>([...checkpoints, 4.999, 9.999, 24.999, 49.999]);
  for (let value = 1; value <= 1_000; value *= 1.005) values.add(value);
  return [...values].sort((a, b) => a - b);
})();

/** Asserts `of(m)` never decreases along the grid. */
function expectNonDecreasing(name: string, of: (multiplier: number) => number): void {
  let previous = Number.NEGATIVE_INFINITY;
  let previousAt = 1;
  for (const multiplier of grid) {
    const value = of(multiplier);
    expect(value, `${name}(${String(multiplier)}) < ${name}(${String(previousAt)})`).toBeGreaterThanOrEqual(previous);
    previous = value;
    previousAt = multiplier;
  }
}

describe("SPEC-0011 escalada visual por multiplicador", () => {
  test.each([
    [1, "day"],
    [5, "sunset"],
    [10, "sunset"],
    [25, "night"],
    [50, "night"],
    [100, "night"],
  ] as const)("SPEC-0011:UT-03 céu em %fx está na faixa %s", (multiplier, band: SkyBand) => {
    expect(skyAt(multiplier, pipaSkyTheme).band).toBe(band);
  });

  test("SPEC-0011:UT-03 céu: paleta do tema no início de cada faixa, abaixo de 5x é dia e abaixo de 25x é pôr do sol", () => {
    expect(skyAt(1, pipaSkyTheme).palette).toEqual(pipaSkyTheme.day);
    expect(skyAt(5, pipaSkyTheme).palette).toEqual(pipaSkyTheme.sunset);
    expect(skyAt(25, pipaSkyTheme).palette).toEqual(pipaSkyTheme.night);
    expect(skyAt(1_000, pipaSkyTheme).palette).toEqual(pipaSkyTheme.night);
    expect(skyAt(4.999, pipaSkyTheme).band).toBe("day");
    expect(skyAt(24.999, pipaSkyTheme).band).toBe("sunset");
    // Continuous blend between the bands: halfway is neither of its ends.
    const between = skyAt(2.5, pipaSkyTheme).palette;
    expect(between).not.toEqual(pipaSkyTheme.day);
    expect(between).not.toEqual(pipaSkyTheme.sunset);
  });

  test("SPEC-0011:UT-03 estrelas: opacidade 0 abaixo de 10x, surgindo entre 10x e 25x, 1 a partir de 25x", () => {
    expect(starsOpacity(1)).toBe(0);
    expect(starsOpacity(5)).toBe(0);
    expect(starsOpacity(9.999)).toBe(0);
    expect(starsOpacity(10)).toBeGreaterThanOrEqual(0);
    expect(starsOpacity(15)).toBeGreaterThan(0);
    expect(starsOpacity(15)).toBeLessThan(1);
    expect(starsOpacity(25)).toBe(1);
    expect(starsOpacity(50)).toBe(1);
    expect(starsOpacity(100)).toBe(1);
  });

  test("SPEC-0011:UT-03 lua visível a partir de 50x", () => {
    for (const multiplier of [1, 5, 10, 25, 49.999]) expect(moonVisible(multiplier), `${String(multiplier)}x`).toBe(false);
    for (const multiplier of [50, 100, 1_000]) expect(moonVisible(multiplier), `${String(multiplier)}x`).toBe(true);
  });

  test("SPEC-0011:UT-03 câmera: altitude = log2(m) telas, limitada a 12 telas", () => {
    for (const multiplier of checkpoints) {
      expect(cameraAltitude(multiplier), `${String(multiplier)}x`).toBeCloseTo(Math.log2(multiplier), 6);
    }
    expect(cameraAltitude(4_096)).toBeCloseTo(12, 6);
    expect(cameraAltitude(10_000)).toBe(12);
  });

  test("SPEC-0011:UT-03 vento: 0,2 + 0,8 · min(1, log10(m)/2)", () => {
    expect(windIntensity(1)).toBeCloseTo(0.2, 6);
    expect(windIntensity(10)).toBeCloseTo(0.6, 6);
    expect(windIntensity(100)).toBeCloseTo(1, 6);
    expect(windIntensity(10_000)).toBeCloseTo(1, 6);
    for (const multiplier of checkpoints) {
      expect(windIntensity(multiplier), `${String(multiplier)}x`).toBeCloseTo(
        0.2 + 0.8 * Math.min(1, Math.log10(multiplier) / 2),
        6,
      );
    }
  });

  test("SPEC-0011:UT-03 céu, estrelas, lua, câmera e vento nunca diminuem com o multiplicador crescente", () => {
    expectNonDecreasing("sky.progress", (multiplier) => skyAt(multiplier, pipaSkyTheme).progress);
    expectNonDecreasing("stars", starsOpacity);
    expectNonDecreasing("moon", (multiplier) => (moonVisible(multiplier) ? 1 : 0));
    expectNonDecreasing("camera", cameraAltitude);
    expectNonDecreasing("wind", windIntensity);
    expect(skyAt(1, pipaSkyTheme).progress).toBe(0);
    expect(skyAt(5, pipaSkyTheme).progress).toBe(1);
    expect(skyAt(25, pipaSkyTheme).progress).toBe(2);
    expect(skyAt(100, pipaSkyTheme).progress).toBe(2);
  });
});
