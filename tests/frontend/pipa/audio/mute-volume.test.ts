import { describe, expect, test } from "vitest";

import { MemoryStorage, newAudioRig, ThrowingStorage, type AudioRig } from "./support/audio";
import {
  defaultSoundPreference,
  masterGain,
  masterGainCeiling,
  masterRampMs,
  soundPreferenceKey,
  type PreferenceStorage,
} from "./support/audio-module";

// SPEC-0018 UT-04: mute and volume. setMuted(true) then setVolume(0.3): read on the OfflineAudioContext double, the
// master gain goes to 0 through a 50 ms ramp and, unmuted, to 0.3 · 0.8 (also through a ramp); the preference is
// written to "apogeu.sound"; with a localStorage that throws the behaviour is the same, without errors.

/** Master gain at `ms` of the audio clock. */
const masterAt = (rig: AudioRig, ms: number): number => rig.context().master().gain.at(ms / 1000);

const stored = (storage: MemoryStorage): unknown => {
  const raw = storage.getItem(soundPreferenceKey);
  return raw === null ? null : JSON.parse(raw);
};

/** Unlocked engine over `storage`, in flight at 2x, with the default preference (volume 0.7). */
async function flyingRig(storage: PreferenceStorage | null): Promise<AudioRig> {
  const rig = newAudioRig({ storage });
  await rig.audio.unlock();
  rig.audio.prepare();
  rig.advance(300);
  rig.audio.takeoff(0);
  rig.audio.setMultiplier(2);
  rig.advance(300);
  return rig;
}

/** setMuted(true) → setVolume(0.3) → setMuted(false), checking the master gain ramps on the audio clock. */
async function checkMuteAndVolume(storage: PreferenceStorage | null): Promise<void> {
  const rig = await flyingRig(storage);
  const before = masterGain(defaultSoundPreference.volume, false);
  expect(masterAt(rig, rig.nowMs())).toBeCloseTo(before, 3);

  const mutedAt = rig.nowMs();
  rig.audio.setMuted(true);
  // A ramp, not a jump: still at the previous gain when muted, halfway down in the middle, 0 after 50 ms.
  expect(masterAt(rig, mutedAt)).toBeCloseTo(before, 2);
  const midway = masterAt(rig, mutedAt + masterRampMs / 2);
  expect(midway).toBeGreaterThan(0.01);
  expect(midway).toBeLessThan(before - 0.01);
  expect(masterAt(rig, mutedAt + masterRampMs)).toBeCloseTo(0, 2);
  rig.advance(100);
  expect(rig.audio.state()).toBe("muted");

  rig.audio.setVolume(0.3);
  rig.advance(100);
  // Still muted: the volume changes nothing that is heard.
  expect(masterAt(rig, rig.nowMs())).toBeCloseTo(0, 3);

  const unmutedAt = rig.nowMs();
  rig.audio.setMuted(false);
  const target = 0.3 * masterGainCeiling;
  expect(masterGain(0.3, false)).toBeCloseTo(target, 10);
  expect(masterAt(rig, unmutedAt)).toBeCloseTo(0, 2);
  const rising = masterAt(rig, unmutedAt + masterRampMs / 2);
  expect(rising).toBeGreaterThan(0.01);
  expect(rising).toBeLessThan(target - 0.01);
  expect(masterAt(rig, unmutedAt + masterRampMs)).toBeCloseTo(target, 2);
  rig.advance(100);
  expect(masterAt(rig, rig.nowMs())).toBeCloseTo(target, 3);
  expect(rig.audio.state()).toBe("running");
}

describe("SPEC-0018 mudo e volume", () => {
  test("SPEC-0018:UT-04 masterGain = volume · (mudo ? 0 : 1) · 0,8", () => {
    expect(masterGain(1, false)).toBeCloseTo(0.8, 10);
    expect(masterGain(0.7, false)).toBeCloseTo(0.56, 10);
    expect(masterGain(0.3, false)).toBeCloseTo(0.24, 10);
    expect(masterGain(1, true)).toBe(0);
    expect(masterGain(0, false)).toBe(0);
  });

  test("SPEC-0018:UT-04 setMuted(true) e setVolume(0,3): ganho mestre a 0 por rampa de 50 ms e, ao desmutar, a 0,3 · 0,8", async () => {
    await checkMuteAndVolume(new MemoryStorage());
  });

  test("SPEC-0018:UT-04 a preferência é gravada em apogeu.sound a cada mudança", async () => {
    const storage = new MemoryStorage();
    const rig = await flyingRig(storage);

    rig.audio.setMuted(true);
    expect(stored(storage)).toEqual({ muted: true, volume: 0.7 });
    rig.audio.setVolume(0.3);
    expect(stored(storage)).toEqual({ muted: true, volume: 0.3 });
    rig.audio.setMuted(false);
    expect(stored(storage)).toEqual({ muted: false, volume: 0.3 });
    expect(rig.audio.preference()).toEqual({ muted: false, volume: 0.3 });
    expect(storage.writes.every((write) => write.key === soundPreferenceKey)).toBe(true);
  });

  test("SPEC-0018:UT-04 a preferência gravada é lembrada ao recriar o áudio (mudo antes mesmo do primeiro gesto)", async () => {
    const storage = new MemoryStorage();
    storage.setItem(soundPreferenceKey, JSON.stringify({ muted: true, volume: 0.4 }));

    const rig = newAudioRig({ storage });
    expect(rig.audio.preference()).toEqual({ muted: true, volume: 0.4 });
    expect(rig.audio.state()).toBe("muted");

    await rig.audio.unlock();
    rig.audio.prepare();
    rig.advance(300);
    expect(rig.audio.state()).toBe("muted");
    expect(masterAt(rig, rig.nowMs())).toBeCloseTo(0, 3);
    expect(rig.audio.cueLog()).toEqual([]);

    rig.audio.setMuted(false);
    rig.advance(100);
    expect(masterAt(rig, rig.nowMs())).toBeCloseTo(0.4 * masterGainCeiling, 3);
  });

  test("SPEC-0018:UT-04 sem preferência ou com valor inválido vale o padrão {muted:false, volume:0,7}", async () => {
    const invalid = new MemoryStorage();
    invalid.setItem(soundPreferenceKey, "{not json");

    for (const storage of [new MemoryStorage(), invalid]) {
      const rig = newAudioRig({ storage });
      expect(rig.audio.preference()).toEqual({ muted: false, volume: 0.7 });
      await rig.audio.unlock();
      rig.audio.prepare();
      rig.advance(300);
      expect(masterAt(rig, rig.nowMs())).toBeCloseTo(0.7 * masterGainCeiling, 3);
    }
  });

  test("SPEC-0018:UT-04 com localStorage lançando exceção o comportamento é o mesmo, sem erro", async () => {
    const storage = new ThrowingStorage();
    const rig = newAudioRig({ storage });
    expect(rig.audio.preference()).toEqual(defaultSoundPreference);

    await checkMuteAndVolume(storage);
  });
});
