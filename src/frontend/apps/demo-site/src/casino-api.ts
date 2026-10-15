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
