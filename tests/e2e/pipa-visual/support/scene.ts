// Shared steps of the SPEC-0011 E2E tests (scene of the Pipa in Chromium with WebGL, against the Aspire environment
// started by tools/ci/run-e2e.sh). The game is opened as in SPEC-0010 (tests/e2e/pipa/support/pipa.ts); the scene is
// read through its part of the test hook (SPEC-0011 §6): `window.__APOGEU_TEST__.scene.freeze(t, state)`,
// `.effectsLog()` and `.frameTimes()` — only in test builds.
import { expect, type Page } from "@playwright/test";

import { waitForState, type HookRoundState } from "../../pipa/support/pipa";

/** State `freeze` positions the scene in (phases of the RoundState of SPEC-0010@1). */
export interface FreezeState {
  readonly phase: "betting" | "flying" | "crashed";
  readonly multiplier: number;
}

/** An entry of the scene's effects log (the fields these tests read). */
export interface LoggedEffect {
  readonly name: string;
  readonly atMs: number;
  readonly style?: string;
  readonly durationMs?: number;
}

interface SceneHook {
  freeze(atMs: number, state: FreezeState): void | Promise<void>;
  effectsLog(): readonly LoggedEffect[];
  frameTimes(): readonly number[];
}

/** What these tests read from `window` (cast inside page.evaluate: the SPEC-0010 support types the hook without `scene`). */
export interface SceneWindow {
  __APOGEU_TEST__?: { scene?: SceneHook };
  __sceneProbe__?: { contextLost: number; contextRestored: number; announcements: string[] };
  __loseContext__?: { loseContext(): void; restoreContext(): void };
  __samePage__?: string;
}

/** Waits for the scene part of the test hook: the SPEC-0011 scene is mounted. */
export async function waitForSceneHook(page: Page): Promise<void> {
  await expect
    .poll(
      () =>
        page.evaluate(() => typeof (window as unknown as SceneWindow).__APOGEU_TEST__?.scene?.freeze === "function"),
      {
        message: "window.__APOGEU_TEST__.scene (SPEC-0011 §6) is installed by the scene of the test build",
        timeout: 30_000,
      },
    )
    .toBe(true);
}

export async function freezeScene(page: Page, atMs: number, state: FreezeState): Promise<void> {
  await page.evaluate(async ([at, frozen]) => {
    const scene = (window as unknown as SceneWindow).__APOGEU_TEST__?.scene;
    if (scene === undefined) throw new Error("window.__APOGEU_TEST__.scene is missing");
    await scene.freeze(at, frozen);
  }, [atMs, state] as const);
}

export function effectsLog(page: Page): Promise<LoggedEffect[]> {
  return page.evaluate(() => [...((window as unknown as SceneWindow).__APOGEU_TEST__?.scene?.effectsLog() ?? [])]);
}

export function frameTimes(page: Page): Promise<number[]> {
  return page.evaluate(() => [...((window as unknown as SceneWindow).__APOGEU_TEST__?.scene?.frameTimes() ?? [])]);
}

/** p-th percentile (nearest rank) of the values. */
export function percentile(values: readonly number[], p: number): number {
  const sorted = [...values].sort((a, b) => a - b);
  const rank = Math.min(sorted.length - 1, Math.max(0, Math.ceil((p / 100) * sorted.length) - 1));
  return sorted[rank] ?? Number.NaN;
}

/**
 * Installs, before the game loads, the counters of WebGL context loss/restore (capture listeners on the document, so
 * any canvas of the page counts) and the record of every text the HUD's aria-live region announces.
 */
export async function installSceneProbe(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const probe = { contextLost: 0, contextRestored: 0, announcements: [] as string[] };
    (window as unknown as SceneWindow).__sceneProbe__ = probe;
    document.addEventListener("webglcontextlost", () => (probe.contextLost += 1), true);
    document.addEventListener("webglcontextrestored", () => (probe.contextRestored += 1), true);
    const record = (): void => {
      for (const region of document.querySelectorAll("[aria-live]")) {
        const announced = (region.textContent ?? "").trim();
        if (announced !== "" && probe.announcements.at(-1) !== announced) probe.announcements.push(announced);
      }
    };
    new MutationObserver(record).observe(document, { subtree: true, childList: true, characterData: true });
  });
}

export function sceneProbe(page: Page): Promise<{ contextLost: number; contextRestored: number; announcements: string[] }> {
  return page.evaluate(() => {
    const probe = (window as unknown as SceneWindow).__sceneProbe__;
    if (probe === undefined) throw new Error("installSceneProbe was not called before the game loaded");
    return { ...probe, announcements: [...probe.announcements] };
  });
}

/** Console errors and uncaught page errors from now on. */
export function collectErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(`console: ${message.text()}`);
  });
  page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
  return errors;
}

/**
 * True when the scene canvas shows a drawn picture: read right after the renderer's frame (a requestAnimationFrame
 * callback registered later runs after it), at least half the pixels opaque and more than one colour.
 */
export function sceneIsDrawn(page: Page): Promise<boolean> {
  return page.evaluate(
    () =>
      new Promise<boolean>((resolve) => {
        requestAnimationFrame(() => {
          const canvas = document.querySelector("canvas");
          if (canvas === null || canvas.width === 0 || canvas.height === 0) {
            resolve(false);
            return;
          }
          const copy = document.createElement("canvas");
          copy.width = 64;
          copy.height = 64;
          const context = copy.getContext("2d");
          if (context === null) {
            resolve(false);
            return;
          }
          context.drawImage(canvas, 0, 0, copy.width, copy.height);
          const { data } = context.getImageData(0, 0, copy.width, copy.height);
          let opaque = 0;
          const colours = new Set<number>();
          for (let index = 0; index < data.length; index += 4) {
            if ((data[index + 3] ?? 0) > 0) opaque += 1;
            colours.add(((data[index] ?? 0) << 16) | ((data[index + 1] ?? 0) << 8) | (data[index + 2] ?? 0));
          }
          resolve(opaque >= (copy.width * copy.height) / 2 && colours.size > 1);
        });
      }),
  );
}

/** The scene canvas uses WebGL (the context the renderer created, webgl2 or webgl) and it is not lost. */
export function webGlAlive(page: Page): Promise<boolean> {
  return page.evaluate(() => {
    const canvas = document.querySelector("canvas");
    const gl = canvas?.getContext("webgl2") ?? canvas?.getContext("webgl") ?? null;
    return gl !== null && !gl.isContextLost();
  });
}

/** A round that started betting after `after`, followed to its crash (any phase sequence the server sends). */
export async function followNextRound(
  page: Page,
  after: HookRoundState,
): Promise<{ readonly betting: HookRoundState; readonly crashed: HookRoundState }> {
  const betting = await waitForState(
    page,
    (state) => state.phase === "betting" && state.roundId !== null && state.roundId !== after.roundId,
    "a new round in betting",
  );
  const crashed = await waitForState(
    page,
    (state) => state.roundId === betting.roundId && (state.phase === "crashed" || state.phase === "annulled"),
    `the end of round ${String(betting.roundNumber)}`,
  );
  return { betting, crashed };
}
