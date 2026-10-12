import { expect, test } from "@playwright/test";

import {
  expectNoAxeViolations,
  openGame,
  roundTimeoutMs,
} from "../support/pipa";
import {
  betText,
  clickBet,
  panelLocator,
  setAmount,
  waitForFreshBetting,
  waitForPanel,
  waitForWallet,
} from "./support/bets";

// SPEC-0026 E2E-05: with the automatic cash-out at 1,01x the round flies and the automatic cash-out is announced (aria-live)
// with its payout (R$ 10,00 × 1,01 = R$ 10,10). A round crashing at 1,00x is a loss and the next round is tried, up to 5.

const maxRounds = 5;

test("SPEC-0026:E2E-05 cash-out automático em 1,01x: retirada automática anunciada (crash em 1,00x tenta a próxima, até 5)", async ({
  page,
}) => {
  test.setTimeout((maxRounds + 1) * roundTimeoutMs);
  await openGame(page);
  await waitForWallet(page);

  const panel = panelLocator(page, 1);
  const toggle = panel.getByLabel(betText.autoCashOut, { exact: true });
  await toggle.click();
  await expect(toggle).toBeChecked();
  await panel.getByLabel(betText.autoTarget, { exact: true }).fill("1,01");
  await expectNoAxeViolations(page, "automatic cash-out enabled");

  let announced = false;
  for (let attempt = 0; attempt < maxRounds && !announced; attempt += 1) {
    await waitForFreshBetting(page);
    await waitForPanel(page, 1, ["idle"]);
    await setAmount(page, 1, "10,00");
    await clickBet(page, 1);
    const placed = await waitForPanel(page, 1, ["placed"], 10_000);
    expect(placed.amount).toBe("10.00");

    const outcome = await waitForPanel(page, 1, [
      "cashed_out",
      "lost",
      "refunded",
    ]);
    if (outcome.status !== "cashed_out") {
      await waitForPanel(page, 1, ["idle"]);
      continue;
    }
    expect(outcome).toMatchObject({
      auto: true,
      multiplier: "1.01",
      payout: "10.10",
    });
    await expect(
      page
        .locator("[aria-live]")
        .filter({ hasText: /Aposta 1: retirada automática de R\$ 10,10/u }),
    ).toHaveCount(1);
    announced = true;
  }
  expect(
    announced,
    `every one of ${String(maxRounds)} rounds crashed at 1,00x`,
  ).toBe(true);
});
