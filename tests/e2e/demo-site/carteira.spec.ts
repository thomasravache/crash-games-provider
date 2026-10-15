import {
  expect,
  test,
  type Frame,
  type Page,
  type Response,
} from "@playwright/test";

import {
  casinoWallet,
  cents,
  enterCasino,
  expectNoAxeViolations,
  formatBrl,
  fromCents,
  gamePage,
  lobby,
  pipaBets,
} from "./support/demo-site";

// SPEC-0047 E2E-01: the demo casino with its own wallet, the demo operator in seamless mode (Development). The player
// enters the casino and sees R$ 3.000,00 in the top bar (GET /casino/balance); opens the Pipa, whose HUD shows the same
// casino balance (the game uses the casino wallet); bets R$ 10,00 and cashes out in the flight (RETIRAR); the casino top
// bar of the game page follows (refresh every 5 s) and, back in the lobby, shows 3.000 − 10 + payout. The payout is read
// from the panel of the game in the iframe: the bets test hook (window.__APOGEU_TEST__.bets(), SPEC-0026 §6, test builds),
// cross-checked with the text "Ganhou R$ …" of the panel. Crash points are random: a round lost before the cash-out costs
// R$ 10,00 more and the next round is tried, as SPEC-0026 E2E-01. The casino screens pass axe (WCAG 2.2 AA).

const maxRounds = 5;
const stake = "10.00";
/** A whole round of the Pipa at most (as tests/e2e/pipa/support/pipa.ts). */
const roundTimeoutMs = 190_000;
const gameLoadTimeoutMs = 30_000;
const gamePath = "/jogo/pipa";

type PanelStatus =
  | "idle"
  | "queued"
  | "placed"
  | "active"
  | "cashing_out"
  | "cashed_out"
  | "lost"
  | "refunded";

/** Panel 1 of the BetsState of SPEC-0026 §6, as the test hook of the game returns it (the fields read here). */
interface HookPanel {
  readonly status: PanelStatus;
  readonly payout: string | null;
  readonly creditStatus: "credited" | "pending" | null;
}

function isBalanceRead(response: Response): boolean {
  return (
    response.request().method() === "GET" &&
    new URL(response.url()).pathname === casinoWallet.balancePath
  );
}

/** Opens the Pipa from its card in the lobby (as SPEC-0045 E2E-01) and returns the frame of the game. */
async function openPipa(page: Page): Promise<Frame> {
  const panel = page.getByRole("tabpanel");
  await expect(panel.getByRole("heading", { name: lobby.pipa })).toBeVisible();
  await panel
    .getByRole("link", { name: lobby.pipa })
    .or(panel.getByRole("button", { name: lobby.pipa }))
    .first()
    .click();
  await expect(page).toHaveURL(new RegExp(`${gamePath}$`, "u"));
  const iframe = page.locator("iframe");
  await expect(iframe).toHaveCount(1);
  const frame = await (await iframe.elementHandle())?.contentFrame();
  if (frame === null || frame === undefined) {
    throw new Error("the game iframe has no frame");
  }
  return frame;
}

/** Panel 1 through the bets hook of the game, or null while the game has not wired it yet. */
async function panelOne(frame: Frame): Promise<HookPanel | null> {
  return frame.evaluate(() => {
    const hook = (
      window as unknown as {
        __APOGEU_TEST__?: {
          bets?: () => { panels: readonly unknown[] } | null;
        };
      }
    ).__APOGEU_TEST__;
    const state = hook?.bets?.() ?? null;
    return (state?.panels[0] ?? null) as HookPanel | null;
  });
}

async function waitForPanel(
  frame: Frame,
  statuses: readonly PanelStatus[],
  timeout = roundTimeoutMs,
): Promise<HookPanel> {
  let matched: HookPanel | null = null;
  await expect
    .poll(
      async () => {
        const panel = await panelOne(frame);
        if (panel !== null && statuses.includes(panel.status)) matched = panel;
        return matched !== null;
      },
      {
        message: `waiting for panel 1 ${statuses.join("|")}`,
        timeout,
        intervals: [50, 100, 250],
      },
    )
    .toBe(true);
  if (matched === null) throw new Error(`never reached: ${statuses.join("|")}`);
  return matched;
}

/** One bet of R$ 10,00 and RETIRAR in the flight; the payout when it landed, null when the round was lost. */
async function betAndCashOut(
  frame: Frame,
): Promise<{ readonly payout: string | null; readonly charged: boolean }> {
  const action = frame.getByTestId(pipaBets.action);
  await waitForPanel(frame, ["idle"]);
  await expect(action).toHaveText(pipaBets.bet, { timeout: roundTimeoutMs });
  await frame.getByTestId(pipaBets.amount).fill("10,00");
  await expect(action).toHaveText("APOSTAR R$ 10,00");
  await expect(action).toBeEnabled();
  await action.click();

  const flying = await waitForPanel(frame, ["active", "lost", "refunded"]);
  if (flying.status === "refunded") return { payout: null, charged: false };
  if (flying.status === "lost") {
    await waitForPanel(frame, ["idle"]);
    return { payout: null, charged: true };
  }
  try {
    await expect(action).toHaveText(pipaBets.cashOut, { timeout: 2_000 });
  } catch {
    // Crashed between "active" and the click: nothing to cash out.
    await waitForPanel(frame, ["lost", "idle"]);
    await waitForPanel(frame, ["idle"]);
    return { payout: null, charged: true };
  }
  await action.click();
  const outcome = await waitForPanel(frame, ["cashed_out", "lost"]);
  if (outcome.status !== "cashed_out" || outcome.payout === null) {
    await waitForPanel(frame, ["idle"]);
    return { payout: null, charged: true };
  }
  return { payout: outcome.payout, charged: true };
}

test("SPEC-0047:E2E-01 aposta R$ 10,00 e RETIRAR no voo no cassino demo em modo seamless: o saldo do topo é 3.000 − 10 + pagamento", async ({
  page,
}) => {
  test.setTimeout((maxRounds + 1) * roundTimeoutMs);

  // The casino top bar shows the casino wallet: R$ 3.000,00 (GET /casino/balance).
  const firstRead = page.waitForResponse(isBalanceRead);
  await enterCasino(page);
  expect((await firstRead).status(), `GET ${casinoWallet.balancePath}`).toBe(
    200,
  );
  await expect(page.getByRole("banner")).toContainText(
    formatBrl(casinoWallet.openingBalance),
    { timeout: casinoWallet.refreshTimeoutMs },
  );
  await expectNoAxeViolations(page, "lobby with the casino balance");

  // The game uses the casino wallet: its HUD shows the casino balance.
  const frame = await openPipa(page);
  await expect(frame.getByTestId(pipaBets.balance)).toContainText(
    formatBrl(casinoWallet.openingBalance),
    { timeout: gameLoadTimeoutMs },
  );

  let charged = 0;
  let payout: string | null = null;
  for (let attempt = 0; attempt < maxRounds && payout === null; attempt += 1) {
    const round = await betAndCashOut(frame);
    if (round.charged) charged += 1;
    payout = round.payout;
  }
  if (payout === null) {
    throw new Error(`no cash out landed in ${String(maxRounds)} rounds`);
  }

  // The payout of the panel, as the player sees it.
  await expect(frame.getByTestId(pipaBets.result)).toHaveText(pipaBets.won);
  await expect(frame.getByTestId(pipaBets.result)).toContainText(
    formatBrl(payout),
  );
  await waitForPanel(frame, ["cashed_out"], 30_000).then((panel) => {
    expect(panel.creditStatus).toBe("credited");
  });

  const expected = fromCents(
    cents(casinoWallet.openingBalance) - cents(stake) * charged + cents(payout),
  );

  // The game and the casino top bar of the game page (refreshed every 5 s) show the casino balance.
  await expect(frame.getByTestId(pipaBets.balance)).toContainText(
    formatBrl(expected),
  );
  await expect(
    page.getByRole("banner", { name: /cassino demo/iu }),
  ).toContainText(formatBrl(expected), {
    timeout: casinoWallet.refreshTimeoutMs,
  });
  await expectNoAxeViolations(page, "game with the casino balance", {
    frames: false,
  });

  // Back in the lobby, the top bar shows 3.000 − 10 + payout.
  await page
    .getByRole("link", { name: gamePage.back })
    .or(page.getByRole("button", { name: gamePage.back }))
    .first()
    .click();
  await expect(page).not.toHaveURL(new RegExp(`${gamePath}$`, "u"));
  await expect(page.getByRole("banner")).toContainText(formatBrl(expected), {
    timeout: casinoWallet.refreshTimeoutMs,
  });
  await expectNoAxeViolations(page, "lobby after the game");
});
