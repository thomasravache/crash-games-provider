import { expect, test } from "@playwright/test";

import { hookState, openGame, roundTimeoutMs, waitForState } from "../pipa/support/pipa";
import { frameTimes, percentile, waitForSceneHook } from "../pipa-visual/support/scene";
import { cueLog, expectAudioState, firstGesture, indexOf, waitForAudioHook } from "./support/audio";

// SPEC-0018 E2E-04: fluidity with the sound on (ADR-0019). Over a whole round in Chromium against the real backend, with
// the sound unlocked and playing, the frame times of the scene (SPEC-0011 test hook) have p95 ≤ 16.7 ms. The p95 is
// attached to the report.

const budgetMs = 16.7;

test("SPEC-0018:E2E-04 rodada completa com som ligado: p95 do tempo de quadro ≤ 16,7 ms", async ({ page, browserName }, testInfo) => {
  test.skip(browserName !== "chromium", "SPEC-0018 E2E-04 measures the frame times in Chromium only");
  test.setTimeout(3 * roundTimeoutMs);
  await openGame(page);
  await waitForSceneHook(page);
  await waitForAudioHook(page);
  await firstGesture(page);
  await expectAudioState(page, "running");

  const before = await hookState(page);
  const betting = await waitForState(
    page,
    (state) => state.phase === "betting" && state.roundId !== null && state.roundId !== before.roundId,
    "a new round in betting",
  );
  const framesAtBetting = (await frameTimes(page)).length;
  const cuesAtBetting = (await cueLog(page)).length;
  await waitForState(
    page,
    (state) => state.roundId === betting.roundId && state.phase === "crashed",
    `the crash of round ${String(betting.roundNumber)}`,
  );
  // The crash script of the sound (until 1.5 s) is part of the round.
  await expect
    .poll(async () => indexOf(await cueLog(page), "silence", cuesAtBetting), {
      message: "the crash script of the sound reached its silence",
      timeout: 5_000,
    })
    .toBeGreaterThanOrEqual(0);
  await page.waitForTimeout(1_500);
  const times = (await frameTimes(page)).slice(framesAtBetting);
  const p95 = percentile(times, 95);
  await testInfo.attach("frame-times-with-sound.json", {
    body: JSON.stringify({ frames: times.length, p95, budgetMs }),
    contentType: "application/json",
  });

  expect((await cueLog(page)).length, "cues played during the round").toBeGreaterThan(cuesAtBetting);
  expect(times.length, "frames rendered during the round").toBeGreaterThan(60);
  expect(p95, `p95 of ${String(times.length)} frame times`).toBeLessThanOrEqual(budgetMs);
});
