import { expect, test } from "@playwright/test";

// SPEC-0018 UT-01, rendered guard: the wind is heard in the betting phase. In the game's real order — the round store
// is already betting (prepare) when the player's first gesture calls unlock() — the PipaAudio engine (loaded from the
// Vite dev server of the game) renders, in Chromium's real OfflineAudioContext, a wind with RMS above -50 dBFS from the
// first 500 ms until the takeoff. A guard against a silent start of the betting wind (hearing review of SPEC-0018: the
// first 6 s of a rendered round were digital silence). Needs no backend.

const sampleRate = 48_000;
const takeoffAtSeconds = 3;
const renderSeconds = 3.5;
const windowMs = 50;
const heardDbfs = -50;

interface RenderedAudio {
  unlock(): Promise<void>;
  prepare(): void;
  takeoff(startedAt: number): void;
  setMultiplier(multiplier: number): void;
  setVolume(volume: number): void;
  cueLog(): readonly { readonly name: string; readonly atMs: number }[];
}

interface AudioModuleWindow {
  __pipaAudioModule__?: {
    createPipaAudio(options: {
      createContext: () => OfflineAudioContext;
      storage: null;
      visibility: null;
    }): RenderedAudio;
  };
  __pipaAudioModuleError__?: string;
}

test("SPEC-0018:UT-01 vento audível nas apostas (sinal renderizado no Chromium): prepare → unlock, RMS > −50 dBFS de 0 a 500 ms e até a decolagem", async ({
  page,
  browserName,
}, testInfo) => {
  test.skip(browserName !== "chromium", "SPEC-0018 renders the real signal in Chromium only");
  const engine = new URL(
    "../../src/frontend/packages/scene-kit/src/audio/index.ts",
    `file://${testInfo.project.testDir}/`,
  ).pathname;

  await page.goto("/");
  await page.addScriptTag({
    type: "module",
    content: `
      import("/@fs${engine}").then(
        (module) => { window.__pipaAudioModule__ = module; },
        (error) => { window.__pipaAudioModuleError__ = String(error); },
      );`,
  });
  await page.waitForFunction(() => {
    const target = window as unknown as AudioModuleWindow;
    return target.__pipaAudioModule__ !== undefined || target.__pipaAudioModuleError__ !== undefined;
  });

  const rendered = await page.evaluate(
    async ({ rate, seconds, takeoffAt, windowLength }) => {
      const target = window as unknown as AudioModuleWindow;
      const module = target.__pipaAudioModule__;
      if (module === undefined) throw new Error(`engine module not loaded: ${target.__pipaAudioModuleError__ ?? "?"}`);
      const context = new OfflineAudioContext(1, Math.ceil(rate * seconds), rate);
      const audio = module.createPipaAudio({ createContext: () => context, storage: null, visibility: null });

      // The script runs at render quanta (128 frames): suspend there, call the engine, resume. unlock() resumes the
      // context itself, so nothing is called after it in the same step.
      const quantum = 128;
      const steps = new Map<number, () => void>();
      const at = (secondsIn: number, run: () => void): void => {
        const frame = Math.max(quantum, Math.ceil((secondsIn * rate) / quantum) * quantum);
        const previous = steps.get(frame);
        steps.set(frame, () => {
          previous?.();
          run();
        });
      };
      at(0, () => {
        audio.setVolume(0.7);
        audio.prepare();
        void audio.unlock();
      });
      at(takeoffAt, () => {
        audio.takeoff(Date.now());
        audio.setMultiplier(1);
      });
      for (const [frame, run] of steps) {
        void context.suspend(frame / rate).then(() => {
          run();
          void context.resume();
        });
      }

      const samples = (await context.startRendering()).getChannelData(0);
      const windowFrames = Math.round((windowLength / 1000) * rate);
      const rmsDbfs: number[] = [];
      for (let start = 0; start + windowFrames <= samples.length; start += windowFrames) {
        let sum = 0;
        for (let index = start; index < start + windowFrames; index += 1) sum += (samples[index] ?? 0) ** 2;
        const rms = Math.sqrt(sum / windowFrames);
        rmsDbfs.push(rms > 0 ? 20 * Math.log10(rms) : -Infinity);
      }
      return { rmsDbfs, cues: audio.cueLog().map((entry) => `${entry.name}@${entry.atMs.toFixed(1)}`) };
    },
    { rate: sampleRate, seconds: renderSeconds, takeoffAt: takeoffAtSeconds, windowLength: windowMs },
  );

  await testInfo.attach("betting-wind.json", {
    body: JSON.stringify(rendered),
    contentType: "application/json",
  });
  expect(rendered.cues.some((cue) => cue.startsWith("wind@")), `cues ${rendered.cues.join(", ")}`).toBe(true);
  const betting = rendered.rmsDbfs.slice(0, Math.floor((takeoffAtSeconds * 1000) / windowMs));
  const firstHalfSecond = betting.slice(0, 500 / windowMs);
  expect(firstHalfSecond.length).toBe(10);
  for (const [index, level] of betting.entries()) {
    expect(
      level,
      `RMS of the betting wind at ${String(index * windowMs)}–${String((index + 1) * windowMs)} ms`,
    ).toBeGreaterThan(heardDbfs);
  }
});
