// Game client (SPEC-0010 §6): exchanges the launch token, connects to the hub with reconnection, estimates the clock
// offset (GetServerTime), joins the sala and maps every hub message to a store event. Resynchronizes from a fresh
// RoundSnapshot after each reconnection.

import type { HistoryEntry } from "./state/round-state";
import { estimateServerOffset, type ServerTimeSample } from "./state/multiplier";
import { createRoundStore, type RoundStore } from "./state/store";
import {
  createSignalRConnection,
  type ConnectHub,
  type RoundsHubConnection,
} from "./transport/hub-connection";
import { exchangeLaunchToken, type FetchLike } from "./transport/session-api";
import type { RoundSnapshot } from "./transport/wire";

export interface GameClientOptions {
  /** Base URL of the API (Host), without a trailing slash; the hub is `${apiBaseUrl}/hubs/rounds`. */
  readonly apiBaseUrl: string;
  /** HTTP client; defaults to the global fetch. */
  readonly fetch?: FetchLike;
  /** Hub connection factory; defaults to createSignalRConnection. */
  readonly connectHub?: ConnectHub;
  /** Local clock (epoch ms); defaults to Date.now. */
  readonly now?: () => number;
}

/** What the session exchange returned that the game may show (nothing personal besides the nickname). */
export interface SessionInfo {
  readonly nickname: string;
  readonly salaId: string;
  readonly currency: string;
  readonly mode: string;
  readonly expiresAt: string;
}

export interface GameClient {
  readonly store: RoundStore;
  /** Exchanges the launch token (rejects with SessionError), connects, syncs the clock and joins the sala. */
  start(launchToken: string): Promise<SessionInfo>;
  stop(): Promise<void>;
}

/** GetServerTime round trips per synchronization; the one with the shortest round trip wins. */
const clockSamples = 3;

function snapshotHistory(snapshot: RoundSnapshot): HistoryEntry[] {
  return snapshot.history.map((entry) => ({
    roundId: entry.roundId,
    roundNumber: entry.roundNumber,
    crashPoint: entry.crashPoint,
  }));
}

export function createGameClient(options: GameClientOptions): GameClient {
  const store = createRoundStore();
  const apiBaseUrl = options.apiBaseUrl.replace(/\/+$/u, "");
  const fetchImpl: FetchLike = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
  const connectHub = options.connectHub ?? createSignalRConnection;
  const now = options.now ?? Date.now;

  let connection: RoundsHubConnection | undefined;
  let salaId = "";
  let stopped = false;
  /** Increments on every (re)synchronization, so a stale one never overwrites a newer snapshot. */
  let generation = 0;

  async function syncClock(hub: RoundsHubConnection): Promise<void> {
    let best: { roundTripMs: number; sample: ServerTimeSample } | undefined;
    for (let index = 0; index < clockSamples; index += 1) {
      const requestSentAtMs = now();
      const { serverTime } = await hub.invoke("GetServerTime");
      const responseReceivedAtMs = now();
      const roundTripMs = responseReceivedAtMs - requestSentAtMs;
      if (best === undefined || roundTripMs < best.roundTripMs) {
        best = { roundTripMs, sample: { requestSentAtMs, responseReceivedAtMs, serverTime } };
      }
    }
    if (best !== undefined) store.setServerOffset(Math.round(estimateServerOffset(best.sample)));
  }

  /** Clock sync + JoinSala; the snapshot replaces the store, then the connection is marked connected. */
  async function synchronize(hub: RoundsHubConnection): Promise<void> {
    generation += 1;
    const mine = generation;
    await syncClock(hub);
    const snapshot = await hub.invoke("JoinSala", salaId);
    if (mine !== generation || stopped) return;
    store.dispatch({
      type: "snapshot",
      salaId: snapshot.sala.id,
      growthK: snapshot.sala.growthK,
      current: snapshot.current,
      history: snapshotHistory(snapshot),
    });
    store.setConnection("connected");
  }

  function listen(hub: RoundsHubConnection): void {
    hub.on("RoundBetting", (message) => {
      store.dispatch({ type: "betting", ...message });
    });
    hub.on("RoundFlying", (message) => {
      store.dispatch({ type: "flying", ...message });
    });
    hub.on("RoundSync", (message) => {
      store.dispatch({ type: "sync", ...message });
    });
    hub.on("RoundCrashed", (message) => {
      store.dispatch({ type: "crashed", ...message });
    });
    hub.on("RoundAnnulled", (message) => {
      store.dispatch({ type: "annulled", ...message });
    });
    hub.onreconnecting(() => {
      if (!stopped) store.setConnection("reconnecting");
    });
    hub.onreconnected(() => {
      if (stopped) return;
      // A new connection is outside the sala group: join again and take the fresh snapshot as the truth.
      synchronize(hub).catch(() => {
        if (!stopped) store.setConnection("disconnected");
      });
    });
    hub.onclose(() => {
      if (!stopped) store.setConnection("disconnected");
    });
  }

  return {
    store,
    async start(launchToken) {
      stopped = false;
      store.setConnection("connecting");
      const session = await exchangeLaunchToken(apiBaseUrl, launchToken, fetchImpl);
      salaId = session.salaId;
      // The session token lives only in this closure (memory), never in storage (SPEC-0010 §5).
      const { sessionToken } = session;
      const hub = connectHub(`${apiBaseUrl}/hubs/rounds`, {
        accessTokenFactory: () => sessionToken,
      });
      connection = hub;
      listen(hub);
      await hub.start();
      await synchronize(hub);
      return {
        nickname: session.player.nickname,
        salaId: session.salaId,
        currency: session.currency,
        mode: session.mode,
        expiresAt: session.expiresAt,
      };
    },
    async stop() {
      stopped = true;
      generation += 1;
      const hub = connection;
      connection = undefined;
      if (hub !== undefined) await hub.stop();
      store.setConnection("disconnected");
    },
  };
}
