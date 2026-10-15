import { expect, test, type Frame, type Page } from "@playwright/test";

import {
  casinoWallet,
  cents,
  demoSiteUrl,
  enterCasino,
  expectNoAxeViolations,
  formatBrl,
  fromCents,
  gamePage,
  lobby,
  pipaBets,
} from "../../e2e/demo-site/support/demo-site";

// SPEC-0066 E2E-01: the wallet journey of SPEC-0047 E2E-01 against the published addresses, over HTTPS (ADR-0029). The
// player enters https://cassino.143-95-218-240.sslip.io and sees R$ 3.000,00; opens the Pipa (game from
// https://jogo.143-95-218-240.sslip.io, api https://api.143-95-218-240.sslip.io); bets R$ 10,00 and cashes out in the
// flight (RETIRAR); back in the lobby the top bar shows 3.000 − 10 + payout; axe finds no violation.
//
// The published game is a production build, without the test hook of SPEC-0026 (window.__APOGEU_TEST__ exists only in
// test builds), so the round is followed by what the player sees: the action button (APOSTAR / CANCELAR / RETIRAR) and
// the result of the panel ("Ganhou R$ …", "Perdeu", "Estornado R$ …"). Crash points are random: a round lost before the
// cash-out costs R$ 10,00 more and the next round is tried.
//
// Runs only with APOGEU_E2E_PUBLISHED=1 (G6, after a deploy), through tests/deploy/e2e/playwright.config.ts.

const published = (
  globalThis as {
    process?: { env: Readonly<Record<string, string | undefined>> };
  }
).process?.env["APOGEU_E2E_PUBLISHED"];

const maxRounds = 5;
const stake = "10.00";
/** A whole round of the Pipa at most (as tests/e2e/pipa/support/pipa.ts). */
const roundTimeoutMs = 190_000;
const gameLoadTimeoutMs = 30_000;
const gamePath = "/jogo/pipa";
const refunded = /^Estornado R\$/u;

type RoundOutcome =
  | { readonly kind: "won"; readonly payout: string }
  | { readonly kind: "lost" }
  | { readonly kind: "refunded" };

/** "R$ 1.234,50" inside a text → "1234.50". */
function amountOf(text: string): string {
  const match = /R\$\s*([\d.]+),(\d{2})/u.exec(text);
  if (match === null) throw new Error(`no amount in "${text}"`);
  return `${(match[1] ?? "0").replaceAll(".", "")}.${match[2] ?? "00"}`;
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

/** Text of a test id in the game, or "" while it is not on the screen. */
async function textOf(frame: Frame, testId: string): Promise<string> {
  const locator = frame.getByTestId(testId);
  return (await locator.count()) === 0
    ? ""
    : ((await locator.first().textContent()) ?? "").trim();
}

/** One bet of R$ 10,00 and RETIRAR in the flight, followed only through what the panel shows. */
async function betAndCashOut(frame: Frame): Promise<RoundOutcome> {
  const action = frame.getByTestId(pipaBets.action);
  await expect(action).toHaveText(pipaBets.bet, { timeout: roundTimeoutMs });
  await frame.getByTestId(pipaBets.amount).fill("10,00");
  await expect(action).toHaveText("APOSTAR R$ 10,00");
  await expect(action).toBeEnabled();
  await action.click();
  // The bet left the idle state (CANCELAR while it waits for the round, RETIRAR in the flight).
  await expect(action).not.toHaveText(pipaBets.bet, { timeout: 10_000 });

  // In the flight (RETIRAR) or the round already over for this bet (APOSTAR again: lost or refunded).
  const seen: { state: "cash-out" | "over" | "waiting" } = { state: "waiting" };
  await expect
    .poll(
      async () => {
        const text = await textOf(frame, pipaBets.action);
        if (pipaBets.cashOut.test(text)) seen.state = "cash-out";
        else if (pipaBets.bet.test(text)) seen.state = "over";
        return seen.state;
      },
      {
        message: "waiting for RETIRAR or the end of the round",
        timeout: roundTimeoutMs,
        intervals: [50, 100, 250],
      },
    )
    .not.toBe("waiting");

  if (seen.state === "cash-out") {
    // A crash between the read and the click turns it into APOSTAR: the result below tells.
    await action.click().catch(() => undefined);
  }

  // The result of the panel: "Ganhou R$ …" after the cash-out, "Perdeu" when it crashed first, "Estornado R$ …".
  const result: { outcome: RoundOutcome | null } = { outcome: null };
  await expect
    .poll(
      async () => {
        const text = await textOf(frame, pipaBets.result);
        if (pipaBets.won.test(text))
          result.outcome = { kind: "won", payout: amountOf(text) };
        else if (text === pipaBets.lost) result.outcome = { kind: "lost" };
        else if (refunded.test(text)) result.outcome = { kind: "refunded" };
        return result.outcome !== null;
      },
      {
        message: "waiting for the result of the bet",
        timeout: roundTimeoutMs,
        intervals: [100, 250],
      },
    )
    .toBe(true);
  if (result.outcome === null) throw new Error("the bet has no result");
  return result.outcome;
}

test.describe("SPEC-0066 ambiente publicado", () => {
  test.skip(
    published !== "1",
    "only against the published environment: APOGEU_E2E_PUBLISHED=1 (G6, after the deploy on the VPS)",
  );

  test("SPEC-0066:E2E-01 nos endereços públicos com HTTPS: entra no cassino, aposta R$ 10,00 na Pipa, RETIRAR no voo e o saldo do topo é 3.000 − 10 + pagamento", async ({
    page,
  }) => {
    test.setTimeout((maxRounds + 1) * roundTimeoutMs);
    expect(demoSiteUrl()).toMatch(/^https:\/\//u);

    // The casino top bar shows the casino wallet: R$ 3.000,00.
    await enterCasino(page);
    await expect(page.getByRole("banner")).toContainText(
      formatBrl(casinoWallet.openingBalance),
      { timeout: casinoWallet.refreshTimeoutMs },
    );
    await expectNoAxeViolations(page, "lobby with the casino balance");

    // The game, served by the public game address, shows the casino balance.
    const frame = await openPipa(page);
    expect(frame.url()).toMatch(/^https:\/\//u);
    await expect(frame.getByTestId(pipaBets.balance)).toContainText(
      formatBrl(casinoWallet.openingBalance),
      { timeout: gameLoadTimeoutMs },
    );

    let charged = 0;
    let payout: string | null = null;
    for (
      let attempt = 0;
      attempt < maxRounds && payout === null;
      attempt += 1
    ) {
      const round = await betAndCashOut(frame);
      if (round.kind !== "refunded") charged += 1;
      if (round.kind === "won") payout = round.payout;
    }
    if (payout === null) {
      throw new Error(`no cash out landed in ${String(maxRounds)} rounds`);
    }

    const expected = fromCents(
      cents(casinoWallet.openingBalance) -
        cents(stake) * charged +
        cents(payout),
    );

    // The game and the casino top bar of the game page (refreshed every 5 s) show the casino balance.
    await expect(frame.getByTestId(pipaBets.balance)).toContainText(
      formatBrl(expected),
      { timeout: casinoWallet.refreshTimeoutMs },
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
});
