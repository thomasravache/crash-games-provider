import { expect, test } from "@playwright/test";

import {
  expectNoAxeViolations,
  hookState,
  openGame,
  roundTimeoutMs,
  waitForState,
} from "../support/pipa";
import {
  actionButton,
  betText,
  betsState,
  clickBet,
  panelLocator,
  panelOf,
  setAmount,
  waitForPanel,
  waitForWallet,
} from "./support/bets";

// SPEC-0026 E2E-04: with the round in flight the player bets on panel 1 (PRÓXIMA: queued for the next round) and, in the
// next betting phase, on panel 2; panel 1 becomes active in that round and both panels evolve independently (panel 1
// cashes out while panel 2 keeps flying). A round that crashes before the cash out lands proves nothing: retried.

const maxRounds = 5;

test("SPEC-0026:E2E-04 aposta no voo fica PRÓXIMA e ativa na rodada seguinte; os dois painéis evoluem de forma independente", async ({
  page,
}) => {
  test.setTimeout((2 * maxRounds + 1) * roundTimeoutMs);
  await openGame(page);
  await waitForWallet(page);

  let proven = false;
  for (let attempt = 0; attempt < maxRounds && !proven; attempt += 1) {
    await waitForPanel(page, 1, ["idle"]);
    await waitForPanel(page, 2, ["idle"]);
    // A round in flight, then panel 1 bets for the next round.
    const flight = await waitForState(
      page,
      (state) => state.phase === "flying",
      "a round in flight",
    );
    await setAmount(page, 1, "10,00");
    await clickBet(page, 1);
    const queued = await waitForPanel(page, 1, ["queued", "placed"], 10_000);
    if (queued.status !== "queued") continue;
    await expect(
      panelLocator(page, 1).getByText(betText.next, { exact: true }),
    ).toBeVisible();
    await expect(actionButton(page, 1)).toHaveText(betText.cancel);
    expect(panelOf(await betsState(page), 2).status).toBe("idle");
    await expectNoAxeViolations(page, "queued bet panel");

    // Next round: panel 1 activated (placed in that round), panel 2 bets in the betting phase.
    const next = await waitForState(
      page,
      (state) => state.phase === "betting" && state.roundId !== flight.roundId,
      "the next round in betting",
    );
    const activated = await waitForPanel(page, 1, ["placed"], 10_000);
    expect(activated.roundId).toBe(next.roundId);
    await expect(
      panelLocator(page, 1).getByText(betText.next, { exact: true }),
    ).toHaveCount(0);
    await setAmount(page, 2, "5,00");
    await clickBet(page, 2);
    const second = await waitForPanel(page, 2, ["placed", "queued"], 10_000);
    if (second.status !== "placed") continue;
    const both = await betsState(page);
    expect(panelOf(both, 1).betId).not.toBe(panelOf(both, 2).betId);
    expect(panelOf(both, 2).roundId).toBe(next.roundId);

    // In flight: cash out panel 1 only.
    const flying = await waitForPanel(page, 1, ["active", "lost", "refunded"]);
    if (flying.status !== "active") continue;
    await expect(actionButton(page, 1)).toHaveText(betText.cashOut);
    await actionButton(page, 1).click();
    const first = await waitForPanel(page, 1, ["cashed_out", "lost"]);
    const round = await hookState(page);
    if (
      first.status !== "cashed_out" ||
      round.phase !== "flying" ||
      round.roundId !== next.roundId
    ) {
      await waitForPanel(page, 2, ["lost", "cashed_out", "refunded"]);
      continue;
    }
    // Panel 2 is untouched by the cash out of panel 1: still in flight with its own bet.
    const afterCashOut = await betsState(page);
    expect(panelOf(afterCashOut, 2)).toMatchObject({
      status: "active",
      betId: second.betId,
    });
    await expect(actionButton(page, 2)).toHaveText(betText.cashOut);

    // At the crash panel 2 loses while panel 1 keeps its win.
    await waitForPanel(page, 2, ["lost"]);
    expect(panelOf(await betsState(page), 1).status).toBe("cashed_out");
    proven = true;
  }
  expect(
    proven,
    `no round flew long enough to show independent panels in ${String(maxRounds)} attempts`,
  ).toBe(true);
});
