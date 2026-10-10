import { sceneResolution, windIntensity } from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

import { newAnimator, playRound } from "./support/scene";

// SPEC-0011 UT-07: performance ceilings (ADR-0019, ADR-0021) in the worst case — 100x (every milestone pulse, maximum
// wind) followed by the crash on a 3x display: ≤ 300 particles, ≤ 3 active filters, pixel density ≤ 2.

describe("SPEC-0011 tetos de desempenho", () => {
  test("SPEC-0011:UT-07 pior caso (100x, vento máximo, crash, tela 3x): partículas ≤ 300, filtros ≤ 3, densidade ≤ 2 em todos os quadros", () => {
    expect(windIntensity(100)).toBeCloseTo(1, 6);
    const animator = newAnimator("full", 3);

    const frames = playRound(animator, 100);

    expect(frames.some((frame) => frame.multiplier === 100)).toBe(true);
    for (const frame of frames) {
      const at = `${frame.phase} at ${String(frame.timeMs)} ms`;
      expect(frame.particles, `particles, ${at}`).toBeLessThanOrEqual(300);
      expect(frame.activeFilters, `filters, ${at}`).toBeLessThanOrEqual(3);
      expect(frame.resolution, `resolution, ${at}`).toBeLessThanOrEqual(2);
    }
  });

  test.each([
    [1, 1],
    [1.5, 1.5],
    [2, 2],
    [3, 2],
    [4, 2],
  ])("SPEC-0011:UT-07 densidade de pixels %f → resolução %f (teto 2)", (devicePixelRatio, resolution) => {
    expect(sceneResolution(devicePixelRatio)).toBe(resolution);
  });
});
