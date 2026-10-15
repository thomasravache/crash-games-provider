import { expect, test, type Locator, type Page } from "@playwright/test";

import {
  expectNoAxeViolations,
  openGame,
  roundTimeoutMs,
  waitForState,
} from "../support/pipa";
import {
  actionButton,
  cashOutAndMeasure,
  clickBet,
  formatBrl,
  setAmount,
  waitForFreshBetting,
  waitForPanel,
  waitForWallet,
  type HookPanel,
} from "../bets/support/bets";

// SPEC-0031 E2E-01: opened by the demo launch, the player bets R$ 10,00, cashes out during the flight and then loses
// another bet; "Todas as apostas" showed the bet during the round (the demo launch has no nickname: "Jogador***") and
// "Minhas apostas" lists both (cashed_out with the payout, lost); axe finds no violation in either tab. Crash points
// are random: a round that crashes before the cash out lands counts as the lost bet, and an annulled round is retried.

const maxRounds = 6;

/** Texts and test ids of SPEC-0031, as packages/game-ui/src/bets/bets-tabs-text.ts. */
const tabsText = {
  all: "Todas as apostas",
  mine: "Minhas apostas",
  won: "Ganhou",
  lost: "Perdeu",
} as const;
const liveBetId = /^live-bet-/u;
const myBetId = (betId: string): string => `my-bet-${betId}`;

const tab = (page: Page, name: string): Locator =>
  page.getByRole("tab", { name, exact: true });
const tabPanel = (page: Page, name: string): Locator =>
  page.getByRole("tabpanel", { name, exact: true });

async function openTab(page: Page, name: string): Promise<Locator> {
  await tab(page, name).click();
  await expect(tab(page, name)).toHaveAttribute("aria-selected", "true");
  const panel = tabPanel(page, name);
  await expect(panel).toBeVisible();
  return panel;
}

/** One bet of R$ 10,00 on panel 1 in a fresh round; checks "Todas as apostas" while the round is on. */
async function betAndWatch(page: Page): Promise<{
  readonly roundId: string | null;
  readonly flying: HookPanel;
}> {
  const betting = await waitForFreshBetting(page);
  await setAmount(page, 1, "10,00");
  await clickBet(page, 1);
  const placed = await waitForPanel(page, 1, ["placed"], 10_000);
  expect(placed.betId).not.toBeNull();

  // The bet of the player shows up in the live bets of the round (anonymized nickname and amount).
  const all = await openTab(page, tabsText.all);
  await expect(
    all
      .getByTestId(liveBetId)
      .filter({ hasText: "Jogador***" })
      .filter({ hasText: "R$ 10,00" })
      .first(),
  ).toBeVisible({ timeout: 10_000 });

  await waitForState(
    page,
    (state) => state.roundId === betting.roundId && state.phase !== "betting",
    `round ${String(betting.roundNumber)} leaving betting`,
  );
  const flying = await waitForPanel(page, 1, ["active", "lost", "refunded"]);
  return { roundId: betting.roundId, flying };
}

test("SPEC-0031:E2E-01 aposta R$ 10,00 retirada no voo e outra perdida: Todas as apostas mostra a aposta na rodada e Minhas apostas lista as duas", async ({
  page,
}) => {
  test.setTimeout((maxRounds + 1) * roundTimeoutMs);
  await openGame(page);
  const opening = await waitForWallet(page);
  expect(opening.balance).toBe("3000.00");

  let won: HookPanel | null = null;
  let lost: HookPanel | null = null;
  let checkedLiveAxe = false;
  for (
    let attempt = 0;
    attempt < maxRounds && (won === null || lost === null);
    attempt += 1
  ) {
    const { flying } = await betAndWatch(page);
    if (!checkedLiveAxe) {
      await expectNoAxeViolations(page, "Todas as apostas tab");
      checkedLiveAxe = true;
    }

    let outcome: HookPanel;
    if (flying.status === "active" && won === null) {
      // First the cash out during the flight.
      outcome = (await cashOutAndMeasure(page, 1)).outcome;
    } else {
      // Then (or when the flight already ended) a bet that is not cashed out.
      outcome = await waitForPanel(page, 1, ["cashed_out", "lost", "refunded"]);
    }
    if (outcome.status === "cashed_out" && won === null) won = outcome;
    else if (outcome.status === "lost" && lost === null) lost = outcome;
    await waitForPanel(page, 1, ["idle"]);
  }
  if (won === null || lost === null)
    throw new Error(
      `no cashed out and lost bet in ${String(maxRounds)} rounds (won: ${String(won !== null)}, lost: ${String(lost !== null)})`,
    );
  const wonBetId = won.betId ?? "";
  const lostBetId = lost.betId ?? "";

  // "Minhas apostas" lists both: the cash out with its payout, and the loss.
  const mine = await openTab(page, tabsText.mine);
  const wonRow = mine.getByTestId(myBetId(wonBetId));
  await expect(wonRow).toBeVisible({ timeout: 10_000 });
  await expect(wonRow).toContainText(tabsText.won);
  await expect(wonRow).toContainText(formatBrl(won.payout ?? ""));
  const lostRow = mine.getByTestId(myBetId(lostBetId));
  await expect(lostRow).toBeVisible();
  await expect(lostRow).toContainText(tabsText.lost);
  await expectNoAxeViolations(page, "Minhas apostas tab");

  // Back to the live bets: the tab still renders and stays accessible.
  await openTab(page, tabsText.all);
  await expect(actionButton(page, 1)).toBeVisible();
  await expectNoAxeViolations(page, "Todas as apostas tab after the history");
});
