import { expect, test } from "@playwright/test";

import { openGame, roundTimeoutMs, waitForCrash, waitForState } from "../pipa/support/pipa";
import {
  audioState,
  cueLog,
  expectAudioState,
  firstGesture,
  preferenceKey,
  soundButton,
  waitForAudioHook,
} from "./support/audio";

// SPEC-0018 E2E-01: with the game open, the player taps "Som" to turn the sound off and opens the game again (a new
// launch, the way a player comes back: the launch token is single use): the button is still off
// (aria-pressed="false"), audio.state() is "muted" and, even after a gesture and a whole round, no cue enters the log.

test("SPEC-0018:E2E-01 som desligado continua desligado ao reabrir o jogo: aria-pressed=false, estado muted e cueLog vazio", async ({
  page,
}) => {
  test.setTimeout(3 * roundTimeoutMs);
  await openGame(page);
  await waitForAudioHook(page);
  const button = soundButton(page);
  await expect(button).toHaveAttribute("aria-pressed", "true");

  await button.click();
  await expect(button).toHaveAttribute("aria-pressed", "false");
  await expectAudioState(page, "muted");
  const saved = await page.evaluate((key) => window.localStorage.getItem(key), preferenceKey);
  expect(JSON.parse(saved ?? "null"), `localStorage "${preferenceKey}"`).toMatchObject({ muted: true });

  // Reopen the game (same browser profile).
  await openGame(page);
  await waitForAudioHook(page);
  await expect(soundButton(page)).toHaveAttribute("aria-pressed", "false");
  expect(await audioState(page)).toBe("muted");

  // A gesture unlocks the audio, but it stays muted: a whole round passes without a cue.
  await firstGesture(page);
  const crashed = await waitForCrash(page);
  const next = await waitForState(
    page,
    (state) => state.phase === "betting" && state.roundId !== crashed.roundId,
    "the next betting phase",
  );
  await waitForState(page, (state) => state.roundId === next.roundId && state.phase === "crashed", "its crash");
  expect(await audioState(page)).toBe("muted");
  expect(await cueLog(page)).toEqual([]);
});
