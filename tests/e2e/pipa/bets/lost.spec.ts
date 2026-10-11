import { expect, test } from "@playwright/test";

import {
  expectNoAxeViolations,
  openGame,
  roundTimeoutMs,
  text,
  waitForState,
} from "../support/pipa";
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

// SPEC-0026 E2E-02 [jornada: aposta-perdida]: the player bets R$ 10,00 and does not cash out; after PIPA CORTADA! the
// panel shows the loss (BetSettled, SPEC-0024@2), the balance is 10,00 lower and the panel goes back to APOSTAR in the
// next round; axe finds no violation in the panel states. An annulled round refunds the bet and the next one is tried.

const maxRounds = 3;

test("SPEC-0026:E2E-02 [jornada: aposta-perdida] aposta R$ 10,00 sem retirar: Perdeu após PIPA CORTADA!, saldo − 10,00 e volta a APOSTAR", async ({
  page,
}) => {
  test.setTimeout((maxRounds + 1) * roundTimeoutMs);
  await openGame(page);
  const opening = await waitForWallet(page);
  const before = opening.balance ?? "";
  expect(before).toBe("3000.00");

  let lostRound: string | null = null;
  for (
    let attempt = 0;
    attempt < maxRounds && lostRound === null;
    attempt += 1
  ) {
    const betting = await waitForFreshBetting(page);
    await setAmount(page, 1, "10,00");
    await clickBet(page, 1);
    await waitForPanel(page, 1, ["placed"], 10_000);
    await expectBalance(page, fromCents(cents(before) - cents("10.00")));

    await waitForState(
      page,
      (state) => state.roundId === betting.roundId && state.phase !== "betting",
      `round ${String(betting.roundNumber)} leaving betting`,
    );
    const flying = await waitForPanel(page, 1, ["active", "lost", "refunded"]);
    if (flying.status === "active") {
      await expect(actionButton(page, 1)).toHaveText(betText.cashOut);
      await expectNoAxeViolations(page, "active bet panel");
    }
    const outcome = await waitForPanel(page, 1, ["lost", "refunded"]);
    if (outcome.status === "refunded") {
      await expectBalance(page, before);
      await waitForPanel(page, 1, ["idle"]);
      continue;
    }
    lostRound = betting.roundId;
  }
  if (lostRound === null)
    throw new Error(`every round of ${String(maxRounds)} was annulled`);

  const crashed = await waitForState(
    page,
    (state) => state.roundId === lostRound && state.phase === "crashed",
    "the crash",
  );
  expect(crashed.crashPoint).not.toBeNull();
  await expect(page.getByText(text.crashed, { exact: true })).toBeVisible();
  await expect(page.getByTestId(betTestId.result(1))).toHaveText(betText.lost);
  await expectBalance(page, fromCents(cents(before) - cents("10.00")));
  await expectNoAxeViolations(page, "lost bet panel");

  // The next round opens: the panel is ready to bet again, the balance unchanged.
  await waitForState(
    page,
    (state) => state.phase === "betting" && state.roundId !== lostRound,
    "the next round",
  );
  await waitForPanel(page, 1, ["idle"], 10_000);
  await expect(actionButton(page, 1)).toHaveText(betText.bet);
  await expect(page.getByTestId(betTestId.result(1))).toHaveCount(0);
  await expectBalance(page, fromCents(cents(before) - cents("10.00")));
});
