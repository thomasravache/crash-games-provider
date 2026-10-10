import { expect, test } from "@playwright/test";

import { openGame, roundTimeoutMs, waitForState } from "../pipa/support/pipa";
import { collectErrors } from "../pipa-visual/support/scene";
import {
  cueLog,
  expectAudioState,
  firstGesture,
  indexOf,
  milestones,
  waitForAudioHook,
  type LoggedCue,
} from "./support/audio";

// SPEC-0018 IT-01: a real round with sound. Against the real backend (Aspire) in Chromium with a fake audio device
// (`--use-fake-device-for-media-stream`, `--autoplay-policy=no-user-gesture-required`, in this test only), a whole
// round after unlock() logs, in order, wind, takeoff, the milestones reached, the crash (snap → fall) and the restart,
// with no console errors.

// The Chromium launch flags of src/frontend/playwright.config.ts (WebGL on SwiftShader) plus the audio flags of IT-01:
// `use.launchOptions` replaces the project's, so the scene keeps the same renderer as every other Chromium test.
const chromiumWebGl = ["--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"];
test.use({
  launchOptions: {
    args: [...chromiumWebGl, "--use-fake-device-for-media-stream", "--autoplay-policy=no-user-gesture-required"],
  },
});

test("SPEC-0018:IT-01 rodada real após unlock(): cueLog com vento, decolagem, marcos, crash e recomeço, em ordem e sem erros de console", async ({
  page,
  browserName,
}) => {
  test.skip(browserName !== "chromium", "SPEC-0018 IT-01 runs in Chromium only (fake audio device flags)");
  test.setTimeout(3 * roundTimeoutMs);
  const errors = collectErrors(page);
  await openGame(page);
  await waitForAudioHook(page);
  await firstGesture(page);
  await expectAudioState(page, "running");

  // Start from the end of the round in progress, so the whole next round is in the log.
  const previous = await waitForState(
    page,
    (state) => state.phase === "crashed" || state.phase === "annulled",
    "the end of the round in progress",
  );
  const from = (await cueLog(page)).length;
  const betting = await waitForState(
    page,
    (state) => state.phase === "betting" && state.roundId !== null && state.roundId !== previous.roundId,
    "a new round in betting",
  );
  const crashed = await waitForState(
    page,
    (state) => state.roundId === betting.roundId && state.phase === "crashed" && state.crashPoint !== null,
    `the crash of round ${String(betting.roundNumber)}`,
  );
  const crashPoint = Number(crashed.crashPoint);
  // The next betting phase plays the restart.
  await expect
    .poll(
      async () => {
        const now = await cueLog(page);
        const snapAt = indexOf(now, "snap", from);
        return snapAt < 0 ? -1 : indexOf(now, "restart", snapAt);
      },
      {
        message: "the restart cue after the crash",
        timeout: roundTimeoutMs,
      },
    )
    .toBeGreaterThan(0);

  const log: LoggedCue[] = (await cueLog(page)).slice(from);
  const summary = log.map((entry) => entry.name).join(" → ");
  const takeoff = indexOf(log, "takeoff");
  const snap = indexOf(log, "snap", takeoff);
  const fall = indexOf(log, "fall", snap);
  const restart = indexOf(log, "restart", fall);
  expect(takeoff, `takeoff in ${summary}`).toBeGreaterThan(0);
  expect(indexOf(log, "wind"), `wind before the takeoff in ${summary}`).toBeGreaterThanOrEqual(0);
  expect(indexOf(log, "wind"), `wind before the takeoff in ${summary}`).toBeLessThan(takeoff);
  expect(snap, `snap after the takeoff in ${summary}`).toBeGreaterThan(takeoff);
  expect(fall, `fall after the snap in ${summary}`).toBeGreaterThan(snap);
  expect(restart, `restart after the fall in ${summary}`).toBeGreaterThan(fall);
  expect(indexOf(log, "takeoff", takeoff + 1), "a single takeoff in the round").toBe(-1);

  // The milestones reached, once each, between the takeoff and the snap.
  const played = log
    .slice(takeoff, snap)
    .filter((entry) => entry.name === "milestone")
    .map((entry) => entry.milestone);
  const surelyReached = milestones.filter((milestone) => milestone * 1.01 < crashPoint);
  const possiblyReached = milestones.filter((milestone) => milestone <= crashPoint);
  expect(played, `milestones of a round crashed at ${String(crashPoint)}x`).toEqual(
    possiblyReached.slice(0, played.length),
  );
  expect(played.length).toBeGreaterThanOrEqual(surelyReached.length);
  expect(log.filter((entry) => entry.name === "milestone").length, "no milestone outside the flight").toBe(played.length);

  expect(errors, "console errors and uncaught page errors").toEqual([]);
});
