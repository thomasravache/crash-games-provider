import { createVerletRope, type Vec2, type WindField } from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

// SPEC-0011 UT-01/UT-02: the kite line and the tail are Verlet ropes with a fixed step (ADR-0021): stable over long
// runs and independent of the frame rate.

const segments = 20;
const segmentLength = 12;
const restLength = segments * segmentLength;
const stepSeconds = 1 / 120;
const anchor: Vec2 = { x: 40, y: 600 };

/** Gusty wind: changes direction and strength over time (deterministic in the simulation time). */
const gustyWind: WindField = (t) => ({
  x: 220 * Math.sin(t * 1.7) + 90 * Math.sin(t * 6.1 + 0.4),
  y: -140 * Math.abs(Math.sin(t * 0.9)) + 40 * Math.cos(t * 4.3),
});

const newRope = () => createVerletRope({ anchor, segments, segmentLength, stepSeconds });

const allFinite = (points: readonly Vec2[]): boolean =>
  points.every((point) => Number.isFinite(point.x) && Number.isFinite(point.y));

describe("SPEC-0011 física da corda (Verlet, passo fixo)", () => {
  test("SPEC-0011:UT-01 corda de 20 segmentos por 10.000 passos de 1/120 s com vento variável: comprimento ± 2% e nenhuma coordenada NaN", () => {
    const rope = newRope();
    expect(rope.points()).toHaveLength(segments + 1);

    let worstDeviation = 0;
    let nonFiniteAtStep: number | null = null;
    for (let step = 1; step <= 10_000; step += 1) {
      rope.step(gustyWind);
      const points = rope.points();
      if (nonFiniteAtStep === null && !allFinite(points)) nonFiniteAtStep = step;
      worstDeviation = Math.max(worstDeviation, Math.abs(rope.length() - restLength) / restLength);
    }

    expect(nonFiniteAtStep, "first step with a NaN/Infinity coordinate").toBeNull();
    expect(worstDeviation, "worst relative deviation of the length").toBeLessThanOrEqual(0.02);
    expect(rope.time()).toBeCloseTo(10_000 * stepSeconds, 6);
    expect(rope.points()[0]).toEqual(anchor);
  });

  test("SPEC-0011:UT-02 1 s simulado com quadros de 1/30 s e de 1/120 s termina nas mesmas posições (± 1 px)", () => {
    const at30fps = newRope();
    const at120fps = newRope();
    const start = at120fps.points().at(-1) ?? anchor;

    for (let frame = 0; frame < 30; frame += 1) at30fps.advance(1 / 30, gustyWind);
    for (let frame = 0; frame < 120; frame += 1) at120fps.advance(1 / 120, gustyWind);

    expect(at30fps.time()).toBeCloseTo(1, 6);
    expect(at120fps.time()).toBeCloseTo(1, 6);
    const slow = at30fps.points();
    const fast = at120fps.points();
    expect(slow).toHaveLength(segments + 1);
    expect(fast).toHaveLength(segments + 1);
    // The rope moved (the comparison is not between two ropes at rest).
    const end = fast.at(-1) ?? start;
    expect(Math.hypot(end.x - start.x, end.y - start.y)).toBeGreaterThan(1);
    slow.forEach((point, index) => {
      const other = fast[index] ?? { x: Number.NaN, y: Number.NaN };
      expect(Math.abs(point.x - other.x), `x of point ${String(index)}`).toBeLessThanOrEqual(1);
      expect(Math.abs(point.y - other.y), `y of point ${String(index)}`).toBeLessThanOrEqual(1);
    });
  });
});
