import { createMilestoneTracker, type EffectEntry } from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

import { flyTo, frameMs, newAnimator, run } from "./support/scene";

// SPEC-0011 UT-04: the milestones 2x, 5x, 10x, 25x, 50x and 100x pulse once per round, even when a resync takes the
// multiplier back below a milestone it already crossed (12x → 11.8x).

const milestones = [2, 5, 10, 25, 50, 100];

/** 1x → 120x growing 1% per tick, with a resync from 12x back to 11.8x on the way. */
function roundWithResync(): number[] {
  const values: number[] = [];
  let resynced = false;
  for (let value = 1; value < 120; value *= 1.01) {
    if (!resynced && value >= 12) {
      values.push(12, 11.8);
      value = 11.8;
      resynced = true;
      continue;
    }
    values.push(value);
  }
  values.push(120);
  return values;
}

const pulses = (log: readonly EffectEntry[]): number[] =>
  log.flatMap((entry) => (entry.name === "pulse" ? [entry.milestone] : []));

describe("SPEC-0011 marcos do multiplicador", () => {
  test("SPEC-0011:UT-04 de 1x a 120x com ressincronização de 12x para 11,8x cada marco dispara exatamente uma vez", () => {
    const tracker = createMilestoneTracker();
    const fired = roundWithResync().flatMap((value) => [...tracker.update(value)]);

    expect(fired).toEqual(milestones);
  });

  test("SPEC-0011:UT-04 uma nova rodada libera os marcos de novo, uma vez por rodada", () => {
    const tracker = createMilestoneTracker();
    const first = [1, 1.5, 2.1, 1.9, 2.2, 3].flatMap((value) => [...tracker.update(value)]);
    tracker.reset();
    const second = [1, 2.05, 1.99, 2.5, 5.5].flatMap((value) => [...tracker.update(value)]);

    expect(first).toEqual([2]);
    expect(second).toEqual([2, 5]);
  });

  test("SPEC-0011:UT-04 a cena pulsa uma vez por marco na rodada com ressincronização e de novo na rodada seguinte", () => {
    const animator = newAnimator("full");
    animator.prepare();
    run(animator, 500);
    animator.takeoff(0);
    run(animator, 700);
    for (const value of roundWithResync()) {
      animator.setMultiplier(value);
      animator.advance(frameMs);
    }
    animator.crash(120);
    run(animator, 2_000);

    expect(pulses(animator.effectsLog())).toEqual(milestones);

    animator.prepare();
    run(animator, 500);
    animator.takeoff(0);
    flyTo(animator, 1, 6);
    expect(pulses(animator.effectsLog())).toEqual([...milestones, 2, 5]);
  });
});
