import { describe, expect, test } from "vitest";

import { flyTo, newAudioRig, peakOver, sixDb, startedCues, type AudioRig } from "./support/audio";
import { masterGainCeiling } from "./support/audio-module";

// SPEC-0018 UT-05: safe peaks (ADR-0020, ADR-0024). The worst case — flight up to 100x at full volume followed by the
// crash — scheduled on the OfflineAudioContext double (amendment IMP-01: the scheduled curve; the rendered signal is
// checked once in Chromium, tests/e2e/pipa-audio/rendered-signal.spec.ts): the master gain never goes above 0.8, and
// there is no jump above 6 dB between consecutive cues (the sources started at the same time form one cue; its level is
// the highest summed gain with which it reaches the destination).

/** Worst case at full volume: takeoff, 1x → 100x, crash at 100x and the whole crash script. */
async function worstCase(): Promise<{ rig: AudioRig; endMs: number }> {
  const rig = newAudioRig();
  rig.audio.setVolume(1);
  await rig.audio.unlock();
  rig.audio.prepare();
  rig.advance(1_000);
  rig.audio.takeoff(0);
  flyTo(rig, 1, 100);
  rig.audio.crash(100);
  rig.advance(2_000);
  return { rig, endMs: rig.nowMs() };
}

describe("SPEC-0018 picos seguros", () => {
  test("SPEC-0018:UT-05 pior caso (100x e crash, volume 1): o ganho mestre nunca passa de 0,8", async () => {
    const { rig, endMs } = await worstCase();
    const master = rig.context().master();

    const peak = peakOver((t) => master.gain.at(t), 0, endMs);
    expect(peak, "highest master gain over the round").toBeLessThanOrEqual(masterGainCeiling + 1e-9);
    for (const event of master.gain.automation()) {
      if ("value" in event) expect(event.value).toBeLessThanOrEqual(masterGainCeiling + 1e-9);
      else for (const value of event.values) expect(value).toBeLessThanOrEqual(masterGainCeiling + 1e-9);
    }
  });

  test("SPEC-0018:UT-05 pior caso: nenhum salto acima de 6 dB entre sinais consecutivos", async () => {
    const { rig, endMs } = await worstCase();
    const audible = startedCues(rig.context(), endMs).filter((cue) => cue.peak > 1e-4);

    expect(audible.length, "cues heard in the worst case").toBeGreaterThanOrEqual(4);
    for (const [index, cue] of audible.entries()) {
      const previous = audible[index - 1];
      if (previous === undefined) continue;
      const ratio = Math.max(cue.peak, previous.peak) / Math.min(cue.peak, previous.peak);
      expect(
        ratio,
        `cue at ${String(cue.startMs)} ms (peak ${cue.peak.toFixed(4)}) after cue at ${String(previous.startMs)} ms (peak ${previous.peak.toFixed(4)})`,
      ).toBeLessThanOrEqual(sixDb);
    }
  });
});
