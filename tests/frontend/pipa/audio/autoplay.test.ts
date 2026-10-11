import { describe, expect, test } from "vitest";

import { flyTo, names, newAudioRig } from "./support/audio";
import { createPipaAudio } from "./support/audio-module";

// SPEC-0018 UT-03: the autoplay policy is respected. Before unlock(), prepare, takeoff and setMultiplier(3) create or
// resume no AudioContext and queue nothing; after unlock() only the current state (flight at 3x) sounds. Without an
// AudioContext (absent, or its creation fails) no call throws.

/** Every GameAudio call, in the order of a round, followed by the controls. */
function driveEverything(audio: ReturnType<typeof createPipaAudio>): void {
  audio.prepare();
  audio.takeoff(Date.now());
  audio.setMultiplier(1.5);
  audio.setMultiplier(3);
  audio.crash(3.2);
  audio.prepare();
  audio.reset();
  audio.setMuted(true);
  audio.setMuted(false);
  audio.setVolume(0.3);
  audio.cueLog();
  audio.state();
}

describe("SPEC-0018 política de autoplay", () => {
  test("SPEC-0018:UT-03 antes de unlock() nenhum AudioContext é criado nem retomado e nada é enfileirado", () => {
    const rig = newAudioRig();

    rig.audio.prepare();
    rig.audio.takeoff(0);
    flyTo(rig, 1, 3);
    rig.audio.setVolume(0.5);

    expect(rig.contexts).toHaveLength(0);
    expect(rig.audio.cueLog()).toEqual([]);
    expect(rig.audio.state()).toBe("locked");
  });

  test("SPEC-0018:UT-03 após unlock() só o estado atual (voo a 3x) soa: sem decolagem, marcos ou vento de apostas atrasados", async () => {
    const rig = newAudioRig();
    rig.audio.prepare();
    rig.audio.takeoff(0);
    flyTo(rig, 1, 3);

    await rig.audio.unlock();
    rig.advance(200);

    expect(rig.contexts).toHaveLength(1);
    const context = rig.context();
    expect(context.state).toBe("running");
    expect(context.calls.map((call) => call.name)).toContain("resume");
    expect(rig.audio.state()).toBe("running");

    const played = names(rig.audio.cueLog());
    // The flight voices of the current state…
    expect(played).toContain("wind");
    expect(played).toContain("tension");
    // …and nothing of what happened before the gesture (the takeoff pull and the 2x milestone were not queued).
    expect(played.filter((name) => name !== "wind" && name !== "tension")).toEqual([]);
    // And it is audible now.
    expect(context.output(rig.nowMs() / 1000)).toBeGreaterThan(0);
  });

  test("SPEC-0018:UT-03 unlock() repetido não cria outro AudioContext", async () => {
    const rig = newAudioRig();
    await rig.audio.unlock();
    await rig.audio.unlock();

    expect(rig.contexts).toHaveLength(1);
  });

  test("SPEC-0018:UT-03 sem AudioContext no navegador (Node, sem createContext) nenhuma chamada lança exceção", async () => {
    expect(typeof (globalThis as { AudioContext?: unknown }).AudioContext).toBe("undefined");
    const audio = createPipaAudio({ storage: null, visibility: null });

    driveEverything(audio);
    await expect(audio.unlock()).resolves.toBeUndefined();
    driveEverything(audio);
    expect(audio.cueLog()).toEqual([]);
    expect(() => {
      audio.dispose();
    }).not.toThrow();
  });

  test("SPEC-0018:UT-03 criação do AudioContext que falha (lança ou devolve null): o jogo segue mudo, sem exceção", async () => {
    const throwing = createPipaAudio({
      storage: null,
      visibility: null,
      createContext: () => {
        throw new Error("NotAllowedError: AudioContext blocked");
      },
    });
    const missing = createPipaAudio({ storage: null, visibility: null, createContext: () => null });

    for (const audio of [throwing, missing]) {
      driveEverything(audio);
      await expect(audio.unlock()).resolves.toBeUndefined();
      driveEverything(audio);
      expect(audio.cueLog()).toEqual([]);
      expect(() => {
        audio.dispose();
      }).not.toThrow();
    }
  });
});
