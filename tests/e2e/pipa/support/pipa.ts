// Shared steps of the SPEC-0010 E2E tests (Pipa demo against the Aspire environment).
//
// Environment: the Host and game-pipa started by Aspire (`dotnet run --project src/backend/AppHost`), with
// APOGEU_API_URL=<base URL of the api resource>. The player enters through GET {api}/dev/launch-demo (SPEC-0009@1),
// which redirects to {gameBaseUrl}/?lt=…; game-pipa must be a test build, so `window.__APOGEU_TEST__` exists.
// The HUD is found by the texts of SPEC-0010 §6, roles, accessible names and test ids (ADR-0009), never by pixels.
import AxeBuilder from "@axe-core/playwright";
import { expect, type APIRequestContext, type Page, type WebSocketRoute } from "@playwright/test";

/** Texts of SPEC-0010 §6 (pt-BR). */
export const text = {
  betting: "PREPARE SUA PIPA",
  flying: "EM VOO",
  crashed: "PIPA CORTADA!",
  reconnecting: "Reconectando…",
  demoBadge: "DEMO",
  betDisabled: "Apostas em breve",
  historyLabel: "Histórico de rodadas",
  fairnessTitle: "Comprovadamente Justo",
  verified: "Verificado",
  sessionExpired: "Sessão expirada. Volte ao cassino para jogar.",
} as const;

/** Test ids of the HUD (packages/game-ui/src/hud-text.ts). */
export const testId = {
  multiplier: "hud-multiplier",
  countdown: "hud-countdown",
  fairnessCrashPoint: "fairness-crash-point",
} as const;

/** Betting 6 s + flight up to the cap (ln(10000)/0.00006 ms ≈ 154 s) + pause 3 s, with margin (SPEC-0008@2). */
export const roundTimeoutMs = 190_000;

/** RoundState of SPEC-0010 §6 as the test hook returns it (the fields these tests read). */
export interface HookRoundState {
  readonly phase: "idle" | "betting" | "flying" | "crashed" | "annulled";
  readonly roundId: string | null;
  readonly roundNumber: number | null;
  readonly multiplier: number;
  readonly crashPoint: string | null;
  readonly history: readonly { readonly roundId: string; readonly roundNumber: number; readonly crashPoint: string }[];
  readonly serverOffsetMs: number;
  readonly connection: "connecting" | "connected" | "reconnecting" | "disconnected";
}

declare global {
  interface Window {
    __APOGEU_TEST__?: { state(): HookRoundState };
  }
}

export function apiBaseUrl(): string {
  const env = (globalThis as { process?: { env: Readonly<Record<string, string | undefined>> } }).process?.env ?? {};
  const value = env["APOGEU_API_URL"];
  if (value === undefined || value === "") {
    throw new Error(
      "APOGEU_API_URL is not set: the SPEC-0010 E2E tests run against the Aspire environment " +
        "(dotnet run --project src/backend/AppHost), with APOGEU_API_URL=<base URL of the api resource>.",
    );
  }
  return value.replace(/\/+$/u, "");
}

/** A fresh demo launch URL ({gameBaseUrl}/?lt=…) from GET /dev/launch-demo, without following the redirect. */
export async function newLaunchUrl(request: APIRequestContext): Promise<string> {
  const api = apiBaseUrl();
  const response = await request.get(`${api}/dev/launch-demo`, { maxRedirects: 0 });
  const location = response.headers()["location"] ?? "";
  expect(response.status(), `GET ${api}/dev/launch-demo must redirect to the launch URL`).toBe(302);
  const launchUrl = new URL(location, api);
  expect(launchUrl.searchParams.get("lt"), "the launch URL carries the launch token").toBeTruthy();
  return launchUrl.toString();
}

/** The player opens /dev/launch-demo and lands in the loaded game (DEMO badge visible). */
export async function openGame(page: Page): Promise<void> {
  await page.goto(`${apiBaseUrl()}/dev/launch-demo`);
  await expect(page.getByText(text.demoBadge, { exact: true })).toBeVisible({ timeout: 30_000 });
}

/** Current RoundState through `window.__APOGEU_TEST__` (only in test builds). */
export async function hookState(page: Page): Promise<HookRoundState> {
  const state = await page.evaluate(() => window.__APOGEU_TEST__?.state() ?? null);
  if (state === null) {
    throw new Error("window.__APOGEU_TEST__ is missing: game-pipa must be served as a test build (SPEC-0010 §6).");
  }
  return state;
}

/** Polls the hook until `accept` holds, then returns that state. */
export async function waitForState(
  page: Page,
  accept: (state: HookRoundState) => boolean,
  what: string,
  timeout = roundTimeoutMs,
): Promise<HookRoundState> {
  let matched: HookRoundState | undefined;
  await expect
    .poll(
      async () => {
        const state = await hookState(page);
        if (accept(state)) matched = state;
        return matched !== undefined;
      },
      { message: `waiting for ${what}`, timeout, intervals: [100, 250, 500] },
    )
    .toBe(true);
  if (matched === undefined) throw new Error(`never reached: ${what}`);
  return matched;
}

/** The next crashed round after `after` (any crashed round when `after` is undefined). */
export function waitForCrash(page: Page, after?: HookRoundState): Promise<HookRoundState> {
  return waitForState(
    page,
    (state) =>
      state.phase === "crashed" &&
      state.crashPoint !== null &&
      (after === undefined || after.phase !== "crashed" || state.roundId !== after.roundId),
    "a crashed round",
  );
}

/** Value of a multiplier shown with 2 decimal places ("2.47x", "2,47x", "10.000,00x" → 2.47 / 10000). */
export function shownMultiplier(shown: string): number {
  const digits = shown.replace(/\D/gu, "");
  if (digits === "") throw new Error(`no multiplier in "${shown}"`);
  return Number(digits) / 100;
}

export async function hudMultiplier(page: Page): Promise<number> {
  return shownMultiplier(await page.getByTestId(testId.multiplier).innerText());
}

let cachedSession: { readonly token: string; readonly expiresAtMs: number } | undefined;

/**
 * A player session token obtained the way a client does (SPEC-0009): GET /dev/launch-demo → `lt` of the launch URL →
 * POST /api/v1/sessions {launchToken}. Cached for the worker while it is valid for at least another minute.
 */
export async function playerSessionToken(request: APIRequestContext): Promise<string> {
  if (cachedSession !== undefined && cachedSession.expiresAtMs - Date.now() > 60_000) return cachedSession.token;
  const launchToken = new URL(await newLaunchUrl(request)).searchParams.get("lt") ?? "";
  const response = await request.post(`${apiBaseUrl()}/api/v1/sessions`, { data: { launchToken } });
  expect(response.status(), "POST /api/v1/sessions must open the player session").toBe(200);
  const body = (await response.json()) as { sessionToken: string; expiresAt: string };
  const expiresAtMs = Date.parse(body.expiresAt);
  cachedSession = { token: body.sessionToken, expiresAtMs: Number.isNaN(expiresAtMs) ? 0 : expiresAtMs };
  return body.sessionToken;
}

/**
 * Crash point the server persisted for a finished round (GET /api/v1/rounds/{roundId}/verification with the player
 * session, SPEC-0008@3).
 */
export async function serverCrashPoint(request: APIRequestContext, roundId: string): Promise<string> {
  const sessionToken = await playerSessionToken(request);
  const response = await request.get(`${apiBaseUrl()}/api/v1/rounds/${encodeURIComponent(roundId)}/verification`, {
    headers: { Authorization: `Bearer ${sessionToken}` },
  });
  expect(response.status(), `verification data of round ${roundId}`).toBe(200);
  const body = (await response.json()) as { crashPoint: string };
  return body.crashPoint;
}

export async function expectNoAxeViolations(page: Page, state: string): Promise<void> {
  const accessibility = await new AxeBuilder({ page }).analyze();
  expect(accessibility.violations, `axe violations in the ${state} state`).toEqual([]);
}

/**
 * Lets a test take the real-time connection down and back (SPEC-0010 E2E-04). Install before the game loads: the hub
 * WebSocket goes through a route that forwards to the server; `down()` closes it and refuses new attempts (WebSocket
 * and the HTTP negotiate/fallback transports) with the browser offline; `up()` restores the network.
 */
export async function controllableNetwork(page: Page): Promise<{ down(): Promise<void>; up(): Promise<void> }> {
  let offline = false;
  const open = new Set<{ page: WebSocketRoute; server: WebSocketRoute }>();
  const hub = /\/hubs\/rounds/u;

  await page.routeWebSocket(hub, async (ws) => {
    if (offline) {
      await ws.close({ code: 1001, reason: "network down (SPEC-0010 E2E-04)" });
      return;
    }
    const pair = { page: ws, server: ws.connectToServer() };
    open.add(pair);
    ws.onClose(() => open.delete(pair));
  });
  await page.route(hub, (route) => (offline ? route.abort("internetdisconnected") : route.continue()));

  return {
    async down() {
      offline = true;
      await page.context().setOffline(true);
      for (const pair of [...open]) {
        open.delete(pair);
        await pair.server.close({ code: 1001, reason: "network down" });
        await pair.page.close({ code: 1001, reason: "network down" });
      }
    },
    async up() {
      offline = false;
      await page.context().setOffline(false);
    },
  };
}
