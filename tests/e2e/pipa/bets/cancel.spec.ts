import { expect, test } from "@playwright/test";

import { openGame, roundTimeoutMs } from "../support/pipa";
import {
  actionButton,
  betText,
  betTestId,
  cents,
  clickBet,
  expectBalance,
  fromCents,
  setAmount,
  waitForFreshBetting,
  waitForPanel,
  waitForWallet,
} from "./support/bets";

// SPEC-0026 E2E-03: in the betting phase the player bets and cancels; the balance goes back to the value before the bet
// (full refund) and the panel back to APOSTAR. If the betting phase closes before the cancel lands (CANCEL_NOT_ALLOWED),
// the next round is tried.

const maxRounds = 3;

test("SPEC-0026:E2E-03 aposta e cancela na fase de apostas: estorno integral e painel de volta a APOSTAR", async ({
  page,
}) => {
  test.setTimeout((maxRounds + 2) * roundTimeoutMs);
  await openGame(page);
  await waitForWallet(page);

  let cancelled = false;
  for (let attempt = 0; attempt < maxRounds && !cancelled; attempt += 1) {
    await waitForFreshBetting(page, 4_000);
    await waitForPanel(page, 1, ["idle"]);
    const before = (await waitForWallet(page)).balance ?? "";

    await setAmount(page, 1, "10,00");
    await clickBet(page, 1);
    await waitForPanel(page, 1, ["placed"], 10_000);
    await expectBalance(page, fromCents(cents(before) - cents("10.00")));

    const cancel = actionButton(page, 1);
    await expect(cancel).toHaveText(betText.cancel);
    await cancel.click();
    const after = await waitForPanel(
      page,
      1,
      ["idle", "active", "lost", "refunded", "cashed_out"],
      15_000,
    );
    if (after.status !== "idle") {
      // Too late: the round took off with the bet. Let it finish and try again.
      await expect(page.getByTestId(betTestId.error(1))).toBeVisible();
      await waitForPanel(page, 1, ["lost", "cashed_out", "refunded"]);
      await waitForPanel(page, 1, ["idle"]);
      continue;
    }
    await expectBalance(page, before);
    await expect(actionButton(page, 1)).toHaveText(betText.bet);
    cancelled = true;
  }
  expect(
    cancelled,
    `no cancel landed in the betting phase in ${String(maxRounds)} rounds`,
  ).toBe(true);
});
