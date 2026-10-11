import { describe, expect, test } from "vitest";

import { flyTo, loggedSince, names, newAudioRig, type AudioRig } from "./support/audio";
import type { CueEntry, CueName } from "./support/audio-module";

// SPEC-0018 UT-06: the crash script. Given crash(3.21), the schedule is snap (≤ 150 ms) → fall (until 1500 ms, a
// descending pitch) → silence; the wind and the line tension reach 0 within 100 ms; and the crash sounds only once
// (a repeated crash and a late tick change nothing).

const crashPoint = 3.21;

/** Unlocked engine flying up to 3.21x, crashed there; returns the crash time and the log/sources before it. */
async function crashedRig(): Promise<{
  rig: AudioRig;
  crashMs: number;
  loggedBefore: number;
  startedBefore: number;
}> {
  const rig = newAudioRig();
  await rig.audio.unlock();
  rig.audio.prepare();
  rig.advance(1_000);
  rig.audio.takeoff(0);
  rig.advance(700);
  flyTo(rig, 1, crashPoint);
  const loggedBefore = rig.audio.cueLog().length;
  const startedBefore = rig.context().started.length;
  const crashMs = rig.nowMs();
  rig.audio.crash(crashPoint);
  return { rig, crashMs, loggedBefore, startedBefore };
}

const only = (entries: readonly CueEntry[], name: CueName): CueEntry => {
  const found = entries.filter((entry) => entry.name === name);
  expect(found, `exactly one "${name}" cue`).toHaveLength(1);
  const [entry] = found;
  if (entry === undefined) throw new Error(`no "${name}" cue`);
  return entry;
};

describe("SPEC-0018 roteiro do crash", () => {
  test("SPEC-0018:UT-06 crash(3.21): ordem snap (≤ 150 ms) → fall (até 1500 ms) → silêncio", async () => {
    const { rig, crashMs, loggedBefore } = await crashedRig();
    rig.advance(2_000);

    const script = loggedSince(rig.audio, loggedBefore);
    expect(names(script)).toEqual(["snap", "fall", "silence"]);
    const snap = only(script, "snap");
    const fall = only(script, "fall");
    const silence = only(script, "silence");

    expect(snap.atMs).toBeGreaterThanOrEqual(crashMs);
    expect(snap.atMs, "the snap follows the crash right away").toBeLessThanOrEqual(crashMs + 20);
    expect(snap.durationMs).toBeDefined();
    expect(snap.durationMs ?? Infinity).toBeLessThanOrEqual(150);
    expect(fall.atMs).toBeGreaterThanOrEqual(snap.atMs);
    expect(fall.atMs + (fall.durationMs ?? Infinity), "the fall ends by 1500 ms").toBeLessThanOrEqual(crashMs + 1_500);
    expect(silence.atMs).toBeGreaterThanOrEqual(fall.atMs);
    expect(silence.atMs).toBeLessThanOrEqual(crashMs + 1_500);
  });

  test("SPEC-0018:UT-06 o agendamento: estalo curto, queda com tom descendente e silêncio depois de 1500 ms", async () => {
    const { rig, crashMs, loggedBefore, startedBefore } = await crashedRig();
    rig.advance(2_000);
    const context = rig.context();
    const script = loggedSince(rig.audio, loggedBefore);
    const snap = only(script, "snap");
    const fall = only(script, "fall");
    const ofCrash = context.started.slice(startedBefore);
    expect(ofCrash.length, "sources of the crash script").toBeGreaterThan(0);

    // The snap: the sources started with it are heard and are silent 150 ms later.
    const snapSources = ofCrash.filter((source) => Math.abs((source.startTime ?? -1) * 1000 - snap.atMs) <= 1);
    expect(snapSources.length, "sources started with the snap").toBeGreaterThan(0);
    const snapLevel = (ms: number): number =>
      snapSources.reduce((sum, source) => sum + context.level(source, ms / 1000), 0);
    expect(Math.max(snapLevel(snap.atMs + 1), snapLevel(snap.atMs + 10))).toBeGreaterThan(0);
    expect(snapLevel(snap.atMs + 150)).toBeLessThanOrEqual(1e-4);

    // The fall: a descending pitch.
    const fallSources = ofCrash.filter((source) => Math.abs((source.startTime ?? -1) * 1000 - fall.atMs) <= 1);
    const fallEnd = fall.atMs + (fall.durationMs ?? 0);
    const descending = fallSources.filter((source) => {
      const pitch = source.pitch();
      return pitch !== undefined && pitch.at(fallEnd / 1000 - 0.01) < pitch.at(fall.atMs / 1000);
    });
    expect(descending.length, "a source of the fall with a descending pitch").toBeGreaterThan(0);

    // Silence from 1500 ms after the crash.
    for (const ms of [crashMs + 1_500, crashMs + 1_750, crashMs + 2_000]) {
      expect(context.output(ms / 1000), `output ${String(ms - crashMs)} ms after the crash`).toBeLessThanOrEqual(1e-4);
    }
  });

  test("SPEC-0018:UT-06 vento e tensão chegam a 0 em ≤ 100 ms após o crash", async () => {
    const { rig, crashMs, startedBefore } = await crashedRig();
    rig.advance(2_000);
    const context = rig.context();
    const flightVoices = context.started.slice(0, startedBefore);

    // Before the crash the flight voices are heard; 100 ms after it, none of them is.
    const levelOf = (ms: number): number =>
      flightVoices.reduce((sum, source) => sum + context.level(source, ms / 1000), 0);
    expect(levelOf(crashMs - 1), "wind and tension just before the crash").toBeGreaterThan(0);
    expect(levelOf(crashMs + 100), "wind and tension 100 ms after the crash").toBeLessThanOrEqual(1e-4);
    expect(levelOf(crashMs + 1_000)).toBeLessThanOrEqual(1e-4);
  });

  test("SPEC-0018:UT-06 o crash soa uma única vez: crash repetido e tique atrasado não tocam de novo", async () => {
    const { rig, loggedBefore, startedBefore } = await crashedRig();
    rig.advance(50);
    const startedAfterFirst = rig.context().started.length;
    rig.audio.crash(crashPoint);
    rig.audio.setMultiplier(3.3);
    rig.advance(50);
    rig.audio.crash(crashPoint);
    rig.advance(2_000);

    expect(names(loggedSince(rig.audio, loggedBefore))).toEqual(["snap", "fall", "silence"]);
    expect(rig.context().started.length, "no source started by the repeated crash").toBe(startedAfterFirst);
    expect(startedAfterFirst).toBeGreaterThan(startedBefore);
  });
});
