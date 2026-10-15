// Shared steps of the demo casino E2E tests (SPEC-0043, SPEC-0044, SPEC-0045, SPEC-0035).
//
// Environment: the Aspire AppHost running (demo-site and DemoCasino among its resources), with
// APOGEU_DEMO_SITE_URL=<base URL of the demo-site resource>. The screens are found by roles and accessible names
// (ADR-0009), never by pixels or CSS classes.
import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";

/** Accessible names of the entry screen (pt-BR, Stitch "Cassino Demo — Entrada"). */
export const entry = {
  nickname: /apelido/iu,
  enter: /entrar/iu,
} as const;

/** Accessible names of the lobby (pt-BR, Stitch "Cassino Demo — Lobby", SPEC-0044 §6). */
export const lobby = {
  pipa: /pipa/iu,
  categories: /categorias/iu,
  all: /^todos$/iu,
  crash: /^crash$/iu,
  news: /^novidades$/iu,
} as const;

/** Accessible names of the game page (pt-BR, Stitch "Cassino Demo — Pipa (Refinado)", SPEC-0045 §6). */
export const gamePage = {
  back: /voltar ao lobby/iu,
  fullscreen: /tela cheia/iu,
  about: /sobre o jogo/iu,
} as const;

/** Fixed "Sobre o jogo" data of the Pipa in the demo-site (SPEC-0045 §4, Emenda v1). */
export const pipaAbout = {
  rtp: /97\s?%/u,
  maxWin: /10\.000\s?x/iu,
  fairness: /comprovadamente justo/iu,
  provider: /apogeu games/iu,
} as const;

/** The iframe sandbox of the contract (SPEC-0045 §6): the minimum the game needs. */
export const gameSandbox = "allow-scripts allow-same-origin allow-popups";

/** Texts of the Pipa HUD inside the iframe (SPEC-0010 §6): the DEMO badge and the round phases. */
export const pipaHud = {
  demoBadge: "DEMO",
  phase: /PREPARE SUA PIPA|EM VOO|PIPA CORTADA!/u,
} as const;

/**
 * The casino wallet (SPEC-0047 §6): the balance in the top bar of the casino (lobby and game page, Stitch "Cassino Demo —
 * Lobby" and "Cassino Demo — Pipa (Refinado)"), read by the demo-site from GET /casino/balance, refreshed on the way back
 * from the game and every 5 s.
 */
export const casinoWallet = {
  balancePath: "/casino/balance",
  openingBalance: "3000.00",
  /** Back from the game or one 5 s refresh, with room for the shared runner. */
  refreshTimeoutMs: 15_000,
} as const;

/** Test ids of the Pipa bet panel inside the iframe (packages/game-ui/src/bets, SPEC-0026 §6). */
export const pipaBets = {
  amount: "bet-amount-1",
  action: "bet-action-1",
  result: "bet-result-1",
  balance: "hud-balance",
  bet: /^APOSTAR R\$/u,
  cashOut: /^RETIRAR R\$/u,
  won: /^Ganhou R\$/u,
  lost: "Perdeu",
} as const;

/** Integer cents of a decimal string ("2990.5" → 299050). */
export function cents(value: string): number {
  const [units = "0", fraction = ""] = value.split(".");
  return Number(units) * 100 + Number(fraction.padEnd(2, "0").slice(0, 2));
}

/** Decimal string of integer cents (299050 → "2990.50"). */
export function fromCents(value: number): string {
  return `${String(Math.trunc(value / 100))}.${String(value % 100).padStart(2, "0")}`;
}

/** "2990.5" → "R$ 2.990,50" (pt-BR, as the casino and the game show money). */
export function formatBrl(value: string): string {
  const [units = "0", fraction = ""] = value.split(".");
  const grouped = units.replace(/\B(?=(\d{3})+(?!\d))/gu, ".");
  return `R$ ${grouped},${fraction.padEnd(2, "0").slice(0, 2)}`;
}

/** Kit art of the Pipa, served by the game front (SPEC-0044 §6 Emenda v1: {GameBaseUrl}/kit/{code}/…). */
export const pipaKit = {
  thumbnail: "/kit/pipa/thumb.png",
  banner: "/kit/pipa/banner.png",
} as const;

/** Name of the casino session cookie (SPEC-0043 §6). */
export const sessionCookie = "casino_session";

export function demoSiteUrl(): string {
  const env =
    (
      globalThis as {
        process?: { env: Readonly<Record<string, string | undefined>> };
      }
    ).process?.env ?? {};
  const value = env["APOGEU_DEMO_SITE_URL"];
  if (value === undefined || value === "") {
    throw new Error(
      "APOGEU_DEMO_SITE_URL is not set: the SPEC-0043 E2E tests run against the Aspire environment " +
        "(dotnet run --project src/backend/AppHost), with APOGEU_DEMO_SITE_URL=<base URL of the demo-site resource>.",
    );
  }
  return value.replace(/\/+$/u, "");
}

/** A unique valid nickname: letters, digits and _, 3–20 characters. */
export function newNickname(): string {
  return `e2e_${Math.random().toString(36).slice(2, 10)}`;
}

/** Enters the casino with a fresh nickname from the entry screen and waits for the lobby (SPEC-0043 E2E-01 steps). */
export async function enterCasino(page: Page): Promise<string> {
  const nickname = newNickname();
  await page.goto(demoSiteUrl());
  await page.getByRole("textbox", { name: entry.nickname }).fill(nickname);
  await page.getByRole("button", { name: entry.enter }).click();
  await expect(page.getByRole("banner")).toContainText(nickname);
  return nickname;
}

/**
 * WCAG 2.2 AA (§5): no axe violation on the current screen. With `frames: false` the content of iframes is not scanned
 * (SPEC-0045: the casino frame is checked, the game inside it has its own tests); the iframe element itself still is.
 */
export async function expectNoAxeViolations(
  page: Page,
  screen: string,
  options: { readonly frames?: boolean } = {},
): Promise<void> {
  const accessibility = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"])
    .options({ iframes: options.frames ?? true })
    .analyze();
  expect(
    accessibility.violations,
    `axe violations on the ${screen} screen`,
  ).toEqual([]);
}
