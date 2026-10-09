import { expect, test } from "@playwright/test";

import {
  openGame,
  roundTimeoutMs,
  serverCrashPoint,
  shownMultiplier,
  testId,
  text,
  waitForCrash,
} from "./support/pipa";

// SPEC-0010 E2E-03 (jornada verificacao-fairness): the player verifies a finished round in the browser and sees
// "Verificado" with the same crash point the server persisted.

test("SPEC-0010:E2E-03 [jornada: verificacao-fairness] Comprovadamente Justo verifica a rodada encerrada com o mesmo crash", async ({
  page,
  request,
}) => {
  test.setTimeout(roundTimeoutMs + 60_000);
  await openGame(page);

  const round = await waitForCrash(page);
  const roundId = round.roundId ?? "";
  const crashPoint = await serverCrashPoint(request, roundId);
  expect(round.crashPoint).toBe(crashPoint);

  await page.getByRole("button", { name: text.fairnessTitle }).click();
  const dialog = page.getByRole("dialog", { name: text.fairnessTitle });
  await expect(dialog).toBeVisible();

  // The player picks that round (each round of the window is named "Rodada <n>", optionally "#<n>").
  const name = new RegExp(`Rodada\\s*#?${String(round.roundNumber)}(?!\\d)`, "u");
  await dialog
    .getByRole("button", { name })
    .or(dialog.getByRole("option", { name }))
    .or(dialog.getByRole("radio", { name }))
    .first()
    .click();

  await expect(dialog.getByText(text.verified, { exact: true })).toBeVisible({ timeout: 30_000 });
  const shown = await dialog.getByTestId(testId.fairnessCrashPoint).innerText();
  expect(shownMultiplier(shown)).toBe(Number(crashPoint));
});
