import { expect, test, type Page } from "@playwright/test";

import { expectNoAxeViolations, openGame } from "../pipa/support/pipa";
import { expectAudioState, soundButton, waitForAudioHook } from "./support/audio";

// SPEC-0018 E2E-03: the sound button of the game header operated by keyboard only (Tab, Enter, Space): it toggles the
// sound, announces the right aria-pressed (true = sound on) and the page has no axe violations.

/** Presses Tab until the sound button has the focus (it is reachable by keyboard). */
async function tabToSoundButton(page: Page): Promise<void> {
  const button = soundButton(page);
  for (let presses = 0; presses < 40; presses += 1) {
    if (await button.evaluate((element) => element === document.activeElement)) return;
    await page.keyboard.press("Tab");
  }
  await expect(button, "the sound button is reachable with Tab").toBeFocused();
}

test("SPEC-0018:E2E-03 botão de som operado só por teclado: alterna, anuncia aria-pressed correto e sem violações axe", async ({
  page,
}) => {
  await openGame(page);
  await waitForAudioHook(page);
  const button = soundButton(page);
  await expect(button).toBeVisible();
  await expect(button).toHaveAttribute("aria-pressed", "true");
  await expectNoAxeViolations(page, "sound on");

  await tabToSoundButton(page);
  await page.keyboard.press("Enter");
  await expect(button).toHaveAttribute("aria-pressed", "false");
  await expect(button).toBeFocused();
  await expectAudioState(page, "muted");
  await expectNoAxeViolations(page, "sound off");

  await page.keyboard.press("Space");
  await expect(button).toHaveAttribute("aria-pressed", "true");
  await expect(button).toBeFocused();
  // The keyboard activation is the player's gesture: the sound is unlocked and on.
  await expectAudioState(page, "running");
  await expectNoAxeViolations(page, "sound on again");
});
