import { expect, test } from "@playwright/test";

import { hookState, openGame, roundTimeoutMs, waitForState } from "../pipa/support/pipa";
import {
  collectErrors,
  effectsLog,
  followNextRound,
  frameTimes,
  installSceneProbe,
  sceneIsDrawn,
  sceneProbe,
  waitForSceneHook,
  webGlAlive,
  type SceneWindow,
} from "./support/scene";

// SPEC-0011 IT-01/IT-02: the scene rendered for real — the backend started by Aspire (tools/ci/run-e2e.sh) and Chromium
// with WebGL — through a whole round, and its recovery from a lost WebGL context without reloading the page (§5).

test("SPEC-0011:IT-01 rodada completa renderizada em WebGL com o backend real, sem erros de console nem webglcontextlost", async ({
  page,
}) => {
  test.setTimeout(2 * roundTimeoutMs + 60_000);
  const errors = collectErrors(page);
  await installSceneProbe(page);
  await openGame(page);
  await waitForSceneHook(page);
  expect(await webGlAlive(page), "the scene canvas renders with WebGL").toBe(true);
  const framesAtStart = (await frameTimes(page)).length;

  const { crashed } = await followNextRound(page, await hookState(page));
  expect(crashed.phase).toBe("crashed");
  // The crash script plays to its end (tumble until 1.8 s, then the fade).
  await expect
    .poll(async () => (await effectsLog(page)).map((entry) => entry.name), {
      message: "the crash script of the round was played by the scene",
      timeout: 5_000,
    })
    .toEqual(expect.arrayContaining(["snap", "tumble", "fade"]));

  expect((await frameTimes(page)).length, "frames rendered during the round").toBeGreaterThan(framesAtStart + 60);
  expect(await sceneIsDrawn(page), "the canvas shows the scene").toBe(true);
  expect((await sceneProbe(page)).contextLost, "webglcontextlost events").toBe(0);
  expect(errors, "console errors and uncaught page errors").toEqual([]);
});

test("SPEC-0011:IT-02 contexto WebGL perdido em voo: a cena é recriada e volta a acompanhar o estado em ≤ 1 s, sem recarregar", async ({
  page,
}) => {
  test.setTimeout(3 * roundTimeoutMs);
  const errors = collectErrors(page);
  await installSceneProbe(page);
  await openGame(page);
  await waitForSceneHook(page);
  await page.evaluate(() => {
    (window as unknown as SceneWindow).__samePage__ = "before the context loss";
  });

  // A round flying long enough to lose and restore the context during the flight.
  const flying = await waitForState(
    page,
    (state) => state.phase === "flying" && state.multiplier >= 1.05,
    "a round in flight",
  );
  expect(await sceneIsDrawn(page), "the scene is drawn before the loss").toBe(true);

  const lost = await page.evaluate(() => {
    const canvas = document.querySelector("canvas");
    const gl = canvas?.getContext("webgl2") ?? canvas?.getContext("webgl") ?? null;
    const extension = gl?.getExtension("WEBGL_lose_context") ?? null;
    if (extension === null) return "WEBGL_lose_context unavailable";
    (window as unknown as SceneWindow).__loseContext__ = extension;
    extension.loseContext();
    return "lost";
  });
  expect(lost).toBe("lost");
  await expect.poll(async () => (await sceneProbe(page)).contextLost, { timeout: 2_000 }).toBe(1);

  await page.evaluate(() => {
    (window as unknown as SceneWindow).__loseContext__?.restoreContext();
  });
  const framesAtRestore = (await frameTimes(page)).length;

  // Recreated within 1 s: a live WebGL context, the scene drawn again and frames rendered after the restore.
  await expect
    .poll(async () => (await webGlAlive(page)) && (await sceneIsDrawn(page)), {
      message: "the scene is recreated and drawn again",
      timeout: 1_000,
      intervals: [50, 100, 100, 200],
    })
    .toBe(true);
  await expect
    .poll(async () => (await frameTimes(page)).length, { timeout: 1_000, intervals: [50, 100] })
    .toBeGreaterThan(framesAtRestore);

  // Same page (not reloaded), still following the live round.
  expect(await page.evaluate(() => (window as unknown as SceneWindow).__samePage__)).toBe("before the context loss");
  const now = await hookState(page);
  expect(now.roundNumber ?? 0).toBeGreaterThanOrEqual(flying.roundNumber ?? 0);
  const loggedAtRestore = (await effectsLog(page)).length;
  const { crashed } = await followNextRound(page, now);
  expect(crashed.phase).toBe("crashed");
  await expect
    .poll(async () => (await effectsLog(page)).slice(loggedAtRestore).map((entry) => entry.name), {
      message: "the recreated scene plays the crash of the next round",
      timeout: 5_000,
    })
    .toContain("snap");
  expect(await sceneIsDrawn(page), "the scene keeps rendering the following rounds").toBe(true);
  expect(errors, "console errors and uncaught page errors").toEqual([]);
});
