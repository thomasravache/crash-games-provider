// Shared steps of the SPEC-0018 E2E and integration tests (sound of the Pipa, against the Aspire environment started by
// tools/ci/run-e2e.sh). The game is opened as in SPEC-0010 (tests/e2e/pipa/support/pipa.ts); the sound is read through
// its part of the test hook (SPEC-0018 §6): `window.__APOGEU_TEST__.audio.cueLog()` and `.state()` — test builds only.
// The sound control is found by its role and accessible name ("Som", aria-pressed = sound on), never by pixels.
import { expect, type Locator, type Page } from "@playwright/test";

import { text } from "../../pipa/support/pipa";

/** State of the sound (SPEC-0018 §6). */
export type AudioHookState = "locked" | "running" | "suspended" | "muted";

/** An entry of the cue log (the fields these tests read). */
export interface LoggedCue {
  readonly name: string;
  readonly atMs: number;
  readonly milestone?: number;
  readonly durationMs?: number;
}

interface AudioHook {
  cueLog(): readonly LoggedCue[];
  state(): AudioHookState;
}

/** What these tests read from `window` (cast inside page.evaluate: the SPEC-0010 support types the hook without `audio`). */
export interface AudioWindow {
  __APOGEU_TEST__?: { audio?: AudioHook };
}

/** Accessible name of the sound button (SPEC-0018 §6). */
export const soundButtonName = "Som";

export const preferenceKey = "apogeu.sound";

/** Milestones of the multiplier with a cue (SPEC-0018 §6). */
export const milestones = [2, 5, 10, 25, 50, 100] as const;

export function soundButton(page: Page): Locator {
  return page.getByRole("button", { name: soundButtonName, exact: true });
}

/** Waits for the audio part of the test hook: the sound engine of the game is mounted. */
export async function waitForAudioHook(page: Page): Promise<void> {
  await expect
    .poll(
      () => page.evaluate(() => typeof (window as unknown as AudioWindow).__APOGEU_TEST__?.audio?.state === "function"),
      {
        message: "window.__APOGEU_TEST__.audio (SPEC-0018 §6) is installed by the sound of the test build",
        timeout: 30_000,
      },
    )
    .toBe(true);
}

export async function audioState(page: Page): Promise<AudioHookState> {
  const state = await page.evaluate(() => (window as unknown as AudioWindow).__APOGEU_TEST__?.audio?.state() ?? null);
  if (state === null) throw new Error("window.__APOGEU_TEST__.audio is missing");
  return state;
}

export function cueLog(page: Page): Promise<LoggedCue[]> {
  return page.evaluate(() => [...((window as unknown as AudioWindow).__APOGEU_TEST__?.audio?.cueLog() ?? [])]);
}

export async function expectAudioState(page: Page, expected: AudioHookState, timeout = 10_000): Promise<void> {
  await expect.poll(() => audioState(page), { message: `audio.state() is "${expected}"`, timeout }).toBe(expected);
}

/**
 * The player's first gesture on a game control that is not the sound button: opens "Comprovadamente Justo" and closes
 * it again ("Fechar"). Any game control unlocks the sound (SPEC-0018 §6).
 */
export async function firstGesture(page: Page): Promise<void> {
  await page.getByRole("button", { name: text.fairnessTitle }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: "Fechar", exact: true }).click();
  await expect(dialog).toBeHidden();
}

/** Index of the first entry named `name` at or after `from` (-1 when there is none). */
export function indexOf(log: readonly LoggedCue[], name: string, from = 0): number {
  for (let index = from; index < log.length; index += 1) if (log[index]?.name === name) return index;
  return -1;
}
