// Shared steps of the SPEC-0026 E2E tests (bet panel of the Pipa against the Aspire environment, started by
// tools/ci/run-e2e.sh). The game is opened as in SPEC-0010 (../../support/pipa.ts); the bets state is read through
// `window.__APOGEU_TEST__.bets()` (test builds only) and the panel is driven by its roles, labels and test ids
// (packages/game-ui/src/bets/bet-text.ts), never by pixels.
import { expect, type Locator, type Page } from "@playwright/test";

import {
  hookState,
  roundTimeoutMs,
  waitForState,
  type HookRoundState,
} from "../../support/pipa";

export type PanelNumber = 1 | 2;

/** Texts of SPEC-0026 §6 (pt-BR), as packages/game-ui/src/bets/bet-text.ts. */
export const betText = {
  bet: /^APOSTAR R\$/u,
  cancel: "CANCELAR",
  next: "PRÓXIMA",
  cashOut: /^RETIRAR R\$/u,
  won: /^Ganhou R\$/u,
  lost: "Perdeu",
  autoCashOut: "Cash-out automático",
  autoTarget: "Alvo do cash-out automático",
  demo: "DEMO",
} as const;

/** Test ids of the panel and the balance (betTestIds). */
export const betTestId = {
  panel: (panel: PanelNumber) => `bet-panel-${String(panel)}`,
  amount: (panel: PanelNumber) => `bet-amount-${String(panel)}`,
  action: (panel: PanelNumber) => `bet-action-${String(panel)}`,
  result: (panel: PanelNumber) => `bet-result-${String(panel)}`,
  error: (panel: PanelNumber) => `bet-error-${String(panel)}`,
  balance: "hud-balance",
  recharge: "bets-recharge",
} as const;

export type HookPanelStatus =
  | "idle"
  | "queued"
  | "placed"
  | "active"
  | "cashing_out"
  | "cashed_out"
  | "lost"
  | "refunded";

/** BetsState of SPEC-0026 §6 as the test hook returns it (the fields these tests read). */
export interface HookPanel {
  readonly panel: PanelNumber;
  readonly status: HookPanelStatus;
  readonly request: string;
  readonly betId: string | null;
  readonly roundId: string | null;
  readonly amount: string | null;
  readonly multiplier: string | null;
  readonly payout: string | null;
  readonly auto: boolean;
  readonly creditStatus: "credited" | "pending" | null;
  readonly error: string | null;
}

export interface HookBetsState {
  readonly panels: readonly [HookPanel, HookPanel];
  readonly balance: string | null;
  readonly limits: { readonly minBet: string; readonly maxBet: string } | null;
}

/** Current BetsState through `window.__APOGEU_TEST__.bets()` (only in test builds with the bet panel). */
export async function betsState(page: Page): Promise<HookBetsState> {
  const state = await page.evaluate(() => {
    const hook = (
      window as unknown as { __APOGEU_TEST__?: { bets?: () => unknown } }
    ).__APOGEU_TEST__;
    return hook?.bets?.() ?? null;
  });
  if (state === null) {
    throw new Error(
      "window.__APOGEU_TEST__.bets is missing: the bet panel (SPEC-0026 §6) is not wired in the test build.",
    );
  }
  return state as HookBetsState;
}

export function panelOf(state: HookBetsState, panel: PanelNumber): HookPanel {
  return state.panels[panel - 1] as HookPanel;
}

/** Polls the bets hook until `accept` holds, then returns that state. */
export async function waitForBets(
  page: Page,
  accept: (state: HookBetsState) => boolean,
  what: string,
  timeout = roundTimeoutMs,
): Promise<HookBetsState> {
  let matched: HookBetsState | undefined;
  await expect
    .poll(
      async () => {
        const state = await betsState(page);
        if (accept(state)) matched = state;
        return matched !== undefined;
      },
      { message: `waiting for ${what}`, timeout, intervals: [50, 100, 250] },
    )
    .toBe(true);
  if (matched === undefined) throw new Error(`never reached: ${what}`);
  return matched;
}

/** Waits until panel `panel` is in one of `statuses`. */
export async function waitForPanel(
  page: Page,
  panel: PanelNumber,
  statuses: readonly HookPanelStatus[],
  timeout = roundTimeoutMs,
): Promise<HookPanel> {
  const state = await waitForBets(
    page,
    (candidate) => statuses.includes(panelOf(candidate, panel).status),
    `panel ${String(panel)} ${statuses.join("|")}`,
    timeout,
  );
  return panelOf(state, panel);
}

export const panelLocator = (page: Page, panel: PanelNumber): Locator =>
  page.getByTestId(betTestId.panel(panel));
export const actionButton = (page: Page, panel: PanelNumber): Locator =>
  page.getByTestId(betTestId.action(panel));

/** The demo wallet was read: balance and limits in the store, and the balance shown with the DEMO badge. */
export async function waitForWallet(page: Page): Promise<HookBetsState> {
  const state = await waitForBets(
    page,
    (candidate) => candidate.balance !== null && candidate.limits !== null,
    "the first wallet read and the bet limits",
    30_000,
  );
  await expect(page.getByTestId(betTestId.balance)).toHaveText(
    `${formatBrl(state.balance ?? "")} · ${betText.demo}`,
  );
  return state;
}

/** Types the amount (pt-BR) in the panel and checks the green button shows it. */
export async function setAmount(
  page: Page,
  panel: PanelNumber,
  amount: string,
): Promise<void> {
  const input = page.getByTestId(betTestId.amount(panel));
  await input.fill(amount);
  await expect(actionButton(page, panel)).toHaveText(`APOSTAR R$ ${amount}`);
}

/** Clicks APOSTAR on the panel. */
export async function clickBet(page: Page, panel: PanelNumber): Promise<void> {
  const button = actionButton(page, panel);
  await expect(button).toHaveText(betText.bet);
  await expect(button).toBeEnabled();
  await button.click();
}

/** The next round entering betting after `after` (any betting round when undefined). */
export function waitForNewBetting(
  page: Page,
  after?: HookRoundState,
): Promise<HookRoundState> {
  return waitForState(
    page,
    (state) =>
      state.phase === "betting" &&
      state.roundId !== null &&
      state.roundId !== after?.roundId,
    "a new round in betting",
  );
}

/** A round in betting with at least `marginMs` left, so a bet and maybe a cancel fit in it. */
export async function waitForFreshBetting(
  page: Page,
  marginMs = 2_500,
): Promise<HookRoundState> {
  for (;;) {
    const current = await hookState(page);
    const fresh = await waitForNewBetting(
      page,
      current.phase === "betting" ? current : undefined,
    );
    const left = await page.evaluate(() => {
      const hook = (
        window as unknown as {
          __APOGEU_TEST__?: {
            state(): { bettingEndsAt?: string | null; serverOffsetMs: number };
          };
        }
      ).__APOGEU_TEST__;
      const state = hook?.state();
      if (state?.bettingEndsAt === undefined || state.bettingEndsAt === null)
        return Number.POSITIVE_INFINITY;
      return (
        Date.parse(state.bettingEndsAt) - (Date.now() + state.serverOffsetMs)
      );
    });
    if (left >= marginMs) return fresh;
  }
}

/** Integer cents of a decimal string ("2990.5" → 299050). */
export function cents(value: string): number {
  const [units = "0", fraction = ""] = value.split(".");
  return Number(units) * 100 + Number(fraction.padEnd(2, "0").slice(0, 2));
}

/** Decimal string of integer cents (299050 → "2990.50"). */
export function fromCents(value: number): string {
  return `${String(Math.trunc(value / 100))}.${String(value % 100).padStart(2, "0")}`;
}

/** "2990.5" → "R$ 2.990,50" (the format of SPEC-0026 UT-04). */
export function formatBrl(value: string): string {
  const [units = "0", fraction = ""] = value.split(".");
  const grouped = units.replace(/\B(?=(\d{3})+(?!\d))/gu, ".");
  return `R$ ${grouped},${fraction.padEnd(2, "0").slice(0, 2)}`;
}

/** The balance in the store and on the screen reach `expected` (decimal string). */
export async function expectBalance(
  page: Page,
  expected: string,
): Promise<void> {
  await waitForBets(
    page,
    (state) =>
      state.balance !== null && cents(state.balance) === cents(expected),
    `balance ${expected}`,
    30_000,
  );
  await expect(page.getByTestId(betTestId.balance)).toHaveText(
    `${formatBrl(expected)} · ${betText.demo}`,
  );
}

/**
 * Clicks RETIRAR and measures, inside the page, the time from the click to the panel result being shown (SPEC-0026 §5:
 * ≤ 150 ms on a local network). A late cash out loses the bet: `won` false and no measure.
 */
export async function cashOutAndMeasure(
  page: Page,
  panel: PanelNumber,
): Promise<{ readonly outcome: HookPanel; readonly elapsedMs: number | null }> {
  const button = actionButton(page, panel);
  await expect(button).toHaveText(betText.cashOut);
  await page.evaluate(
    ({ actionId, resultId }) => {
      const target = window as unknown as {
        __cashOutTiming?: { clickedAt: number | null; shownAt: number | null };
      };
      const timing: { clickedAt: number | null; shownAt: number | null } = {
        clickedAt: null,
        shownAt: null,
      };
      target.__cashOutTiming = timing;
      document.addEventListener(
        "click",
        (event) => {
          if (
            (event.target as Element | null)?.closest(
              `[data-testid="${actionId}"]`,
            ) !== null
          ) {
            timing.clickedAt = performance.now();
          }
        },
        { capture: true, once: true },
      );
      const observer = new MutationObserver(() => {
        const text =
          document.querySelector(`[data-testid="${resultId}"]`)?.textContent ??
          "";
        if (timing.clickedAt !== null && text.startsWith("Ganhou")) {
          timing.shownAt = performance.now();
          observer.disconnect();
        }
      });
      observer.observe(document.body, {
        subtree: true,
        childList: true,
        characterData: true,
      });
    },
    { actionId: betTestId.action(panel), resultId: betTestId.result(panel) },
  );
  await button.click();
  const outcome = await waitForPanel(
    page,
    panel,
    ["cashed_out", "lost"],
    roundTimeoutMs,
  );
  if (outcome.status !== "cashed_out") return { outcome, elapsedMs: null };
  await expect(page.getByTestId(betTestId.result(panel))).toHaveText(
    betText.won,
  );
  const timing = await page.evaluate(() => {
    const target = window as unknown as {
      __cashOutTiming?: { clickedAt: number | null; shownAt: number | null };
    };
    return target.__cashOutTiming ?? null;
  });
  const elapsedMs =
    timing === null || timing.clickedAt === null || timing.shownAt === null
      ? null
      : timing.shownAt - timing.clickedAt;
  return { outcome, elapsedMs };
}
