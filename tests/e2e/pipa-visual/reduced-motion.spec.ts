import { expect, test } from "@playwright/test";

import { hookState, openGame, roundTimeoutMs } from "../pipa/support/pipa";
import { effectsLog, followNextRound, installSceneProbe, sceneProbe, waitForSceneHook } from "./support/scene";

// SPEC-0011 E2E-02: with the browser asking for reduced motion (prefers-reduced-motion: reduce, ADR-0020/ADR-0021),
// a whole round against the real backend plays no shake, no flash and no parallax, its transitions are crossfades, and
// the HUD still announces every state in its aria-live region.

test.use({ reducedMotion: "reduce" });

/** Rounds tried until one is seen flying (a crash at 1.00x may never be announced as in flight). */
const maxRounds = 3;

test("SPEC-0011:E2E-02 movimento reduzido: rodada completa sem tremor, clarão nem paralaxe, com os estados anunciados no aria-live", async ({
  page,
}) => {
  test.setTimeout((maxRounds + 1) * roundTimeoutMs);
  await installSceneProbe(page);
  await openGame(page);
  await waitForSceneHook(page);
  expect(await page.evaluate(() => matchMedia("(prefers-reduced-motion: reduce)").matches)).toBe(true);

  let announced: string[] = [];
  let roundNumber: number | null = null;
  let crashPoint = "";
  let after = await hookState(page);
  for (let attempt = 0; attempt < maxRounds; attempt += 1) {
    const { betting, crashed } = await followNextRound(page, after);
    after = crashed;
    if (crashed.phase !== "crashed") continue;
    const all = (await sceneProbe(page)).announcements;
    const start = all.lastIndexOf(`Rodada ${String(betting.roundNumber)}: prepare sua pipa.`);
    announced = start < 0 ? [] : all.slice(start);
    roundNumber = betting.roundNumber;
    crashPoint = crashed.crashPoint ?? "";
    if (announced.includes("A pipa está em voo.")) break;
  }

  // The states of the round, in order, in the aria-live region (SPEC-0010 §6 texts).
  expect(roundNumber, "a crashed round was followed").not.toBeNull();
  expect(announced[0]).toBe(`Rodada ${String(roundNumber)}: prepare sua pipa.`);
  expect(announced).toContain("A pipa está em voo.");
  const crashAnnouncement = announced.find((text) => text.startsWith("Pipa cortada em "));
  expect(crashAnnouncement, `crash at ${crashPoint} announced`).toBeDefined();
  expect(announced.indexOf("A pipa está em voo.")).toBeLessThan(announced.indexOf(crashAnnouncement ?? ""));

  // The scene played the round with the reduced profile: no shake, flash nor parallax; crossfade transitions.
  await expect
    .poll(async () => (await effectsLog(page)).map((entry) => entry.name), {
      message: "the crash of the round was played by the scene",
      timeout: 5_000,
    })
    .toContain("snap");
  const log = await effectsLog(page);
  const names = log.map((entry) => entry.name);
  expect(names).not.toContain("shake");
  expect(names).not.toContain("flash");
  expect(names).not.toContain("parallax");
  const transitions = log.filter((entry) => entry.name === "transition");
  expect(transitions.length, "transitions of the scene").toBeGreaterThan(0);
  for (const transition of transitions) {
    expect(transition).toMatchObject({ style: "crossfade", durationMs: 300 });
  }
});
