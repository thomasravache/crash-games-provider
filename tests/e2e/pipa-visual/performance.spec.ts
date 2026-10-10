import { expect, test, type Page } from "@playwright/test";

import { hookState, openGame, roundTimeoutMs, waitForState } from "../pipa/support/pipa";
import { effectsLog, frameTimes, percentile, waitForSceneHook } from "./support/scene";

// SPEC-0011 E2E-03: fluidity (ADR-0019, ADR-0021) — the frame times of the scene, read from the test hook over a whole
// round in Chromium against the real backend, have p95 ≤ 16.7 ms; with the CPU 4x slower (CDP, an approximation of a
// mid-range Android), p95 ≤ 33 ms. The p95 values are attached to the report (the evidence of §8 Observability).

/** Frame times from the start of the next betting phase to the end of that round's crash script. */
async function roundFrameTimes(page: Page): Promise<number[]> {
  const before = await hookState(page);
  const betting = await waitForState(
    page,
    (state) => state.phase === "betting" && state.roundId !== null && state.roundId !== before.roundId,
    "a new round in betting",
  );
  const framesAtBetting = (await frameTimes(page)).length;
  const fadesBefore = (await effectsLog(page)).filter((entry) => entry.name === "fade").length;
  await waitForState(
    page,
    (state) => state.roundId === betting.roundId && state.phase === "crashed",
    `the crash of round ${String(betting.roundNumber)}`,
  );
  // The crash script (tumble until 1.8 s, then the fade) is part of the round.
  await expect
    .poll(async () => (await effectsLog(page)).filter((entry) => entry.name === "fade").length, {
      message: "the crash script reached its fade",
      timeout: 5_000,
    })
    .toBeGreaterThan(fadesBefore);
  return (await frameTimes(page)).slice(framesAtBetting);
}

for (const { cpuSlowdown, budgetMs } of [
  { cpuSlowdown: 1, budgetMs: 16.7 },
  { cpuSlowdown: 4, budgetMs: 33 },
] as const) {
  test(`SPEC-0011:E2E-03 rodada completa com CPU ${String(cpuSlowdown)}x: p95 do tempo de quadro ≤ ${String(budgetMs)} ms`, async ({
    page,
  }, testInfo) => {
    test.setTimeout(3 * roundTimeoutMs);
    await openGame(page);
    await waitForSceneHook(page);
    if (cpuSlowdown > 1) {
      const cdp = await page.context().newCDPSession(page);
      await cdp.send("Emulation.setCPUThrottlingRate", { rate: cpuSlowdown });
    }

    const times = await roundFrameTimes(page);
    const p95 = percentile(times, 95);
    await testInfo.attach(`frame-times-cpu-${String(cpuSlowdown)}x.json`, {
      body: JSON.stringify({ frames: times.length, p95, budgetMs }),
      contentType: "application/json",
    });

    expect(times.length, "frames rendered during the round").toBeGreaterThan(60);
    expect(p95, `p95 of ${String(times.length)} frame times`).toBeLessThanOrEqual(budgetMs);
  });
}
