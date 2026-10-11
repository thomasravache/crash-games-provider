import { describe, expect, test } from "vitest";

import { flyTo, frameMs, newAudioRig } from "./support/audio";
import { createCueScheduler, milestoneHz } from "./support/audio-module";

// SPEC-0018 UT-02: in a round from 1x to 120x with a resync from 12x back to 11.8x, each of the 6 milestones (2x, 5x,
// 10x, 25x, 50x, 100x) sounds exactly once and their pitches are strictly increasing (milestoneHz(i) = 523.25·2^(2i/12)).

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

const strictlyIncreasing = (values: readonly number[]): boolean =>
  values.every((value, index) => index === 0 || value > (values[index - 1] ?? Infinity));

describe("SPEC-0018 marcos sonoros", () => {
  test("SPEC-0018:UT-02 milestoneHz sobe um tom por marco a partir de 523,25 Hz", () => {
    const pitches = milestones.map((_, index) => milestoneHz(index));

    expect(pitches[0]).toBeCloseTo(523.25, 6);
    for (const [index, hz] of pitches.entries()) expect(hz).toBeCloseTo(523.25 * 2 ** ((2 * index) / 12), 6);
    expect(strictlyIncreasing(pitches)).toBe(true);
  });

  test("SPEC-0018:UT-02 de 1x a 120x com ressincronização de 12x para 11,8x cada marco toca uma vez, com altura crescente", () => {
    const scheduler = createCueScheduler();
    const cues = roundWithResync().flatMap((value) => [...scheduler.update(value)]);

    expect(cues.map((cue) => cue.milestone)).toEqual(milestones);
    expect(cues.map((cue) => cue.index)).toEqual([0, 1, 2, 3, 4, 5]);
    for (const cue of cues) expect(cue.hz).toBeCloseTo(milestoneHz(cue.index), 6);
    expect(strictlyIncreasing(cues.map((cue) => cue.hz))).toBe(true);
  });

  test("SPEC-0018:UT-02 uma nova rodada libera os marcos de novo, uma vez por rodada", () => {
    const scheduler = createCueScheduler();
    const first = [1, 1.5, 2.1, 1.9, 2.2, 3].flatMap((value) => scheduler.update(value).map((cue) => cue.milestone));
    scheduler.reset();
    const second = [1, 2.05, 1.99, 2.5, 5.5].flatMap((value) => scheduler.update(value).map((cue) => cue.milestone));

    expect(first).toEqual([2]);
    expect(second).toEqual([2, 5]);
  });

  test("SPEC-0018:UT-02 o PipaAudio toca cada marco uma vez na rodada com ressincronização, com altura crescente", async () => {
    const rig = newAudioRig();
    await rig.audio.unlock();
    rig.audio.prepare();
    rig.advance(500);
    rig.audio.takeoff(0);
    for (const value of roundWithResync()) {
      rig.audio.setMultiplier(value);
      rig.advance(frameMs);
    }

    const played = rig.audio.cueLog().filter((entry) => entry.name === "milestone");
    expect(played.map((entry) => entry.milestone)).toEqual(milestones);
    const pitches = played.map((entry) => entry.hz ?? Number.NaN);
    for (const [index, hz] of pitches.entries()) expect(hz).toBeCloseTo(milestoneHz(index), 6);
    expect(strictlyIncreasing(pitches)).toBe(true);

    // Next round: the milestones sound again, once.
    rig.audio.crash(120);
    rig.advance(2_000);
    rig.audio.prepare();
    rig.advance(500);
    rig.audio.takeoff(0);
    flyTo(rig, 1, 6);
    const next = rig.audio
      .cueLog()
      .filter((entry) => entry.name === "milestone")
      .slice(played.length);
    expect(next.map((entry) => entry.milestone)).toEqual([2, 5]);
  });
});
