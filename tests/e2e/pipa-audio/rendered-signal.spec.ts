import { expect, test } from "@playwright/test";

// SPEC-0018 UT-05, rendered signal (amendment IMP-01: run once in Chromium, with the browser's real
// OfflineAudioContext). The worst case — flight from 1x to 100x at full volume followed by the crash — is rendered by
// the PipaAudio engine itself (loaded from the Vite dev server of the game) and the samples are analysed: the peak never
// goes above the master ceiling of 0.8, and there is no jump above 6 dB between consecutive 50 ms windows that are
// heard (RMS above -50 dBFS). Needs no backend: only the game's dev server of the Playwright config.

const sampleRate = 48_000;
const renderSeconds = 4.4;
const crashAtSeconds = 2.6;
const windowMs = 50;
const masterCeiling = 0.8;
const sixDb = 10 ** (6 / 20);
const heard = 10 ** (-50 / 20);

/** What the page script reads from the engine module (scene-kit/src/audio, served by Vite). */
interface RenderedAudio {
  unlock(): Promise<void>;
  takeoff(startedAt: number): void;
  setMultiplier(multiplier: number): void;
  crash(point: number): void;
  setMuted(muted: boolean): void;
  setVolume(volume: number): void;
  cueLog(): readonly { readonly name: string }[];
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

test("SPEC-0018:UT-05 sinal renderizado no Chromium (OfflineAudioContext real): pico ≤ 0,8 e nenhum salto > 6 dB", async ({
  page,
  browserName,
}, testInfo) => {
  test.skip(browserName !== "chromium", "SPEC-0018 UT-05 renders the real signal once, in Chromium");
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
    async ({ rate, seconds, crashAt, windowLength }) => {
      const target = window as unknown as AudioModuleWindow;
      const module = target.__pipaAudioModule__;
      if (module === undefined) throw new Error(`engine module not loaded: ${target.__pipaAudioModuleError__ ?? "?"}`);
      const context = new OfflineAudioContext(1, Math.ceil(rate * seconds), rate);
      const audio = module.createPipaAudio({ createContext: () => context, storage: null, visibility: null });

      // The script runs at render quanta (128 frames): suspend there, call the engine, resume.
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
      // unlock() resumes the context itself: an OfflineAudioContext then renders ahead at once, so it gets a step of
      // its own and nothing is scheduled after it in the same step (a deterministic harness).
      at(0, () => {
        audio.setMuted(false);
        audio.setVolume(1);
        void audio.unlock();
      });
      at(0.01, () => {
        audio.takeoff(Date.now());
        audio.setMultiplier(1);
      });
      const flightStart = 0.05;
      const flightEnd = crashAt - 0.05;
      for (let time = flightStart; time <= flightEnd; time += 0.02) {
        const multiplier = 100 ** ((time - flightStart) / (flightEnd - flightStart));
        at(time, () => {
          audio.setMultiplier(multiplier);
        });
      }
      at(crashAt, () => {
        audio.setMultiplier(100);
        audio.crash(100);
      });
      for (const [frame, run] of steps) {
        void context.suspend(frame / rate).then(() => {
          run();
          void context.resume();
        });
      }

      const buffer = await context.startRendering();
      const samples = buffer.getChannelData(0);
      let peak = 0;
      for (const sample of samples) peak = Math.max(peak, Math.abs(sample));
      const windowFrames = Math.round((windowLength / 1000) * rate);
      const rms: number[] = [];
      for (let start = 0; start + windowFrames <= samples.length; start += windowFrames) {
        let sum = 0;
        for (let index = start; index < start + windowFrames; index += 1) sum += (samples[index] ?? 0) ** 2;
        rms.push(Math.sqrt(sum / windowFrames));
      }
      return { peak, rms, cues: audio.cueLog().map((entry) => entry.name) };
    },
    { rate: sampleRate, seconds: renderSeconds, crashAt: crashAtSeconds, windowLength: windowMs },
  );

  await testInfo.attach("rendered-signal.json", {
    body: JSON.stringify({ peak: rendered.peak, rms: rendered.rms, cues: rendered.cues }),
    contentType: "application/json",
  });
  expect(rendered.cues, "cues of the rendered worst case").toEqual(
    expect.arrayContaining(["takeoff", "milestone", "snap", "fall"]),
  );
  expect(rendered.peak, "the worst case is heard").toBeGreaterThan(0.01);
  expect(rendered.peak, "peak of the rendered signal").toBeLessThanOrEqual(masterCeiling);
  for (const [index, level] of rendered.rms.entries()) {
    const previous = rendered.rms[index - 1];
    if (previous === undefined || previous < heard || level < heard) continue;
    expect(
      level / previous,
      `RMS jump at ${String(index * windowMs)} ms (${previous.toFixed(4)} → ${level.toFixed(4)})`,
    ).toBeLessThanOrEqual(sixDb);
  }
});
