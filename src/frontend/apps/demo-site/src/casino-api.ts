// Client of the casino BFF (SPEC-0043 §6). Same origin only: the session lives in the HttpOnly casino_session cookie,
// which the page script never reads; no operator credential exists in the browser (ADR-0027).

export const sessionPath = "/casino/session";
const mePath = "/casino/me";
const logoutPath = "/casino/logout";

export const supportedCurrency = "BRL";

export interface CasinoPlayer {
  readonly playerId: string;
  readonly nickname: string;
  readonly currency: string;
}

/** Error codes of the contract, plus the failures of the network itself. */
export type EntryErrorCode = "INVALID_NICKNAME" | "CURRENCY_NOT_SUPPORTED" | "UNAVAILABLE";

export type EntryResult =
  | { readonly ok: true; readonly player: CasinoPlayer }
  | { readonly ok: false; readonly code: EntryErrorCode };

/** Nickname rule of the contract: 3–20 letters, digits or _. */
export const nicknamePattern = /^[A-Za-z0-9_]{3,20}$/u;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function stringField(value: Record<string, unknown>, name: string): string | undefined {
  const field = value[name];
  return typeof field === "string" ? field : undefined;
}

async function readJson(response: Response): Promise<Record<string, unknown> | undefined> {
  try {
    const body: unknown = await response.json();
    return isRecord(body) ? body : undefined;
  } catch {
    return undefined;
  }
}

export async function enterCasino(nickname: string, currency: string): Promise<EntryResult> {
  let response: Response;
  try {
    response = await fetch(sessionPath, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ nickname, currency }),
    });
  } catch {
    return { ok: false, code: "UNAVAILABLE" };
  }

  const body = await readJson(response);
  if (response.ok && body !== undefined) {
    const playerId = stringField(body, "playerId");
    const entered = stringField(body, "nickname");
    if (playerId !== undefined && entered !== undefined) {
      return { ok: true, player: { playerId, nickname: entered, currency } };
    }
  }

  const code = body === undefined ? undefined : stringField(body, "code");
  return code === "INVALID_NICKNAME" || code === "CURRENCY_NOT_SUPPORTED"
    ? { ok: false, code }
    : { ok: false, code: "UNAVAILABLE" };
}

/** The player of the current session, or null without one. */
export async function currentPlayer(signal: AbortSignal): Promise<CasinoPlayer | null> {
  const response = await fetch(mePath, { credentials: "same-origin", signal });
  if (!response.ok) {
    return null;
  }
  const body = await readJson(response);
  if (body === undefined) {
    return null;
  }
  const playerId = stringField(body, "playerId");
  const nickname = stringField(body, "nickname");
  const currency = stringField(body, "currency");
  return playerId === undefined || nickname === undefined || currency === undefined
    ? null
    : { playerId, nickname, currency };
}

export async function leaveCasino(): Promise<void> {
  await fetch(logoutPath, { method: "POST", credentials: "same-origin" });
}

const gamesPath = "/casino/games";

/** A game of the lobby (SPEC-0044 §6): the catalog of the Apogeu as the casino BFF serves it. */
export interface LobbyGame {
  readonly code: string;
  readonly name: string;
  readonly thumbnailUrl: string;
  readonly bannerUrl: string;
  readonly category: string;
  readonly isNew: boolean;
}

/** Outcome of the lobby catalog: the games, no session (401) or the provider unavailable (502 or any other failure). */
export type GamesResult =
  | { readonly status: "ok"; readonly games: readonly LobbyGame[] }
  | { readonly status: "unauthorized" }
  | { readonly status: "unavailable" };

function lobbyGame(value: unknown): LobbyGame | undefined {
  if (!isRecord(value)) {
    return undefined;
  }
  const code = stringField(value, "code");
  const name = stringField(value, "name");
  const thumbnailUrl = stringField(value, "thumbnailUrl");
  const bannerUrl = stringField(value, "bannerUrl");
  const category = stringField(value, "category");
  const isNew = value["isNew"];
  return code === undefined ||
    name === undefined ||
    thumbnailUrl === undefined ||
    bannerUrl === undefined ||
    category === undefined ||
    typeof isNew !== "boolean"
    ? undefined
    : { code, name, thumbnailUrl, bannerUrl, category, isNew };
}

/** GET /casino/games: the games enabled for the casino (the BFF signs the call to the Apogeu, ADR-0027). */
export async function listGames(signal: AbortSignal): Promise<GamesResult> {
  let response: Response;
  try {
    response = await fetch(gamesPath, { credentials: "same-origin", signal });
  } catch {
    return { status: "unavailable" };
  }
  if (response.status === 401) {
    return { status: "unauthorized" };
  }
  if (!response.ok) {
    return { status: "unavailable" };
  }

  let body: unknown;
  try {
    body = await response.json();
  } catch {
    return { status: "unavailable" };
  }
  if (!Array.isArray(body)) {
    return { status: "unavailable" };
  }
  const items: readonly unknown[] = body;
  const games = items.map(lobbyGame);
  return games.every((game) => game !== undefined)
    ? { status: "ok", games }
    : { status: "unavailable" };
}

/** Outcome of the launch (SPEC-0045 §6): the single-use launch URL, no session, the game unavailable or the provider down. */
export type PlayResult =
  | { readonly status: "ok"; readonly launchUrl: string }
  | { readonly status: "unauthorized" }
  | { readonly status: "not-available" }
  | { readonly status: "unavailable" };

/** Only an absolute http(s) URL goes into the iframe. */
function launchUrlOf(body: Record<string, unknown> | undefined): string | undefined {
  const value = body === undefined ? undefined : stringField(body, "launchUrl");
  if (value === undefined || !URL.canParse(value)) {
    return undefined;
  }
  const { protocol } = new URL(value);
  return protocol === "https:" || protocol === "http:" ? value : undefined;
}

/**
 * POST /casino/play/{gameCode}: the casino BFF launches the game at the Apogeu (signed on the server, ADR-0027) and
 * answers the single-use launch URL; the browser never calls the operator API.
 */
export async function playGame(gameCode: string): Promise<PlayResult> {
  let response: Response;
  try {
    response = await fetch(`/casino/play/${encodeURIComponent(gameCode)}`, {
      method: "POST",
      credentials: "same-origin",
    });
  } catch {
    return { status: "unavailable" };
  }
  if (response.status === 401) {
    return { status: "unauthorized" };
  }
  const body = await readJson(response);
  if (response.ok) {
    const launchUrl = launchUrlOf(body);
    return launchUrl === undefined ? { status: "unavailable" } : { status: "ok", launchUrl };
  }
  return response.status === 404 &&
    body !== undefined &&
    stringField(body, "code") === "GAME_NOT_AVAILABLE"
    ? { status: "not-available" }
    : { status: "unavailable" };
}
