import { crashTimeline, type EffectEntry, type SceneFrame } from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

import { flyTo, newAnimator, run } from "./support/scene";

// SPEC-0011 UT-06: the crash script — snap (0 ms) → flash (0–150 ms, opacity ≤ 0.25, once; WCAG 2.3.1) → shake
// (0–300 ms, ≤ 8 px) → tumble (300–1800 ms) → fade.

describe("SPEC-0011 roteiro do crash", () => {
  test("SPEC-0011:UT-06 a linha do tempo do crash é snap → flash → shake → tumble → fade com os tempos do contrato", () => {
    const steps = crashTimeline("full");

    expect(steps.map((step) => step.name)).toEqual(["snap", "flash", "shake", "tumble", "fade"]);
    const starts = steps.map((step) => step.startMs);
    expect(starts).toEqual([...starts].sort((a, b) => a - b));
    const [snap, flash, shake, tumble, fade] = steps;
    if (snap === undefined || flash === undefined || shake === undefined || tumble === undefined || fade === undefined) {
      throw new Error("incomplete crash timeline");
    }
    expect(snap).toMatchObject({ startMs: 0, endMs: 0 });
    expect(flash.startMs).toBe(0);
    expect(flash.endMs).toBeGreaterThan(0);
    expect(flash.endMs).toBeLessThanOrEqual(150);
    expect(flash.intensity).toBeGreaterThan(0);
    expect(flash.intensity).toBeLessThanOrEqual(0.25);
    expect(shake.startMs).toBe(0);
    expect(shake.endMs).toBeLessThanOrEqual(300);
    expect(shake.intensity).toBeGreaterThan(0);
    expect(shake.intensity).toBeLessThanOrEqual(8);
    expect(tumble).toMatchObject({ startMs: 300, endMs: 1_800 });
    expect(fade.startMs).toBeGreaterThanOrEqual(shake.endMs);
    expect(fade.endMs).toBeGreaterThan(fade.startMs);
  });

  test("SPEC-0011:UT-06 crash(3.21): um único clarão, de até 150 ms e opacidade ≤ 0,25, e tremor ≤ 8 px só nos primeiros 300 ms", () => {
    const animator = newAnimator("full");
    animator.prepare();
    run(animator, 500);
    animator.takeoff(0);
    run(animator, 700);
    flyTo(animator, 1, 3.21);
    const crashedAt = animator.frame().timeMs;

    animator.crash(3.21);
    const frames: SceneFrame[] = run(animator, 2_500, 5);

    const flashes = animator.effectsLog().filter((entry): entry is Extract<EffectEntry, { name: "flash" }> =>
      entry.name === "flash",
    );
    expect(flashes).toHaveLength(1);
    expect(flashes[0]?.opacity).toBeLessThanOrEqual(0.25);
    expect(flashes[0]?.durationMs).toBeLessThanOrEqual(150);

    const since = (frame: SceneFrame): number => frame.timeMs - crashedAt;
    const lit = frames.filter((frame) => frame.flashOpacity > 0);
    expect(lit.length, "frames with the flash on").toBeGreaterThan(0);
    for (const frame of frames) expect(frame.flashOpacity).toBeLessThanOrEqual(0.25);
    for (const frame of lit) expect(since(frame), "flash after 150 ms").toBeLessThanOrEqual(150);
    // A single flash: the lit frames are consecutive.
    const firstLit = frames.findIndex((frame) => frame.flashOpacity > 0);
    expect(frames.slice(firstLit, firstLit + lit.length)).toEqual(lit);

    for (const frame of frames) {
      const amplitude = Math.hypot(frame.shake.x, frame.shake.y);
      expect(amplitude, `shake at +${String(since(frame))} ms`).toBeLessThanOrEqual(8);
      if (since(frame) > 300) expect(amplitude, `shake at +${String(since(frame))} ms`).toBe(0);
    }
    expect(frames.at(-1)?.multiplier).toBe(3.21);
    const order = animator
      .effectsLog()
      .map((entry) => entry.name)
      .filter((name) => ["snap", "flash", "shake", "tumble", "fade"].includes(name));
    expect(order).toEqual(["snap", "flash", "shake", "tumble", "fade"]);
  });
});
