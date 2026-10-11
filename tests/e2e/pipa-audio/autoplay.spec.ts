import { expect, test } from "@playwright/test";

import { hookState, openGame, roundTimeoutMs } from "../pipa/support/pipa";
import { followNextRound } from "../pipa-visual/support/scene";
import { audioState, cueLog, expectAudioState, firstGesture, waitForAudioHook } from "./support/audio";

// SPEC-0018 E2E-02: the autoplay policy. With the game open and no gesture from the player, a whole round passes:
// audio.state() is "locked" and the cue log is empty; after the first touch it becomes "running".

test("SPEC-0018:E2E-02 sem gesto do jogador uma rodada inteira passa em silêncio (locked, cueLog vazio); após o primeiro toque, running", async ({
  page,
}) => {
  test.setTimeout(3 * roundTimeoutMs);
  await openGame(page);
  await waitForAudioHook(page);
  expect(await audioState(page)).toBe("locked");

  await followNextRound(page, await hookState(page));
  expect(await audioState(page)).toBe("locked");
  expect(await cueLog(page)).toEqual([]);

  await firstGesture(page);
  await expectAudioState(page, "running");
});
