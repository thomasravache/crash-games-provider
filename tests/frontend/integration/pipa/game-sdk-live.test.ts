import {
  createGameClient,
  createSignalRConnection,
  exchangeLaunchToken,
  SessionError,
  type RoundPhase,
  type RoundState,
  type SessionInfo,
} from "@apogeu/game-sdk";
import { describe, expect, test } from "vitest";

import { exportHubManifest, hubSchema, openApiSchema } from "./support/contracts";
import { apiBaseUrl, launchDemo, waitUntil, type LaunchDemo } from "./support/environment";
import { schemaErrors } from "./support/json-schema";
import { HttpRecorder, HubRecorder } from "./support/recording";

// SPEC-0010 IT-01, CT-02, CT-03: the real game-sdk in Node (no browser) against the Host started by Aspire. One live
// flow (launch → session → hub → one complete round) is recorded once and shared: IT-01 asserts what the store and
// the hub delivered; CT-02 validates every hub result and message against the RoundsHub contract (SPEC-0008@2);
// CT-03 validates the session exchange against the OpenAPI contract (SPEC-0009@1).
//
// Run: APOGEU_API_URL=<base URL of the api resource> pnpm -C src/frontend test:integration
// (Aspire up with `dotnet run --project src/backend/AppHost`; CT-02 also needs `dotnet build Apogeu.slnx`).

/** Betting 6 s + flight up to the cap (ln(10000)/0.00006 ms ≈ 154 s) + pause 3 s, with margin. */
const roundTimeoutMs = 190_000;
/** Waiting for the next betting phase, then following that round to the crash. */
const flowTimeoutMs = 2 * roundTimeoutMs + 30_000;

interface PhaseSeen {
  readonly roundId: string | null;
  readonly phase: RoundPhase;
}

interface LiveRound {
  readonly api: string;
  readonly launch: LaunchDemo;
  readonly session: SessionInfo;
  readonly http: HttpRecorder;
  readonly hub: HubRecorder;
  readonly roundId: string;
  readonly phases: readonly PhaseSeen[];
  readonly finalState: RoundState;
}

let liveRound: Promise<LiveRound> | undefined;

/** The shared live flow, started by whichever test needs it first. */
function followLiveRound(): Promise<LiveRound> {
  liveRound ??= runLiveRound();
  return liveRound;
}

async function runLiveRound(): Promise<LiveRound> {
  const api = apiBaseUrl();
  const launch = await launchDemo(api);
  if (launch.status !== 302 || launch.launchToken === "") {
    throw new Error(
      `GET ${api}/dev/launch-demo must answer 302 to {gameBaseUrl}/?lt=… (SPEC-0009@1); got ${String(launch.status)} ` +
        `location="${launch.location}"`,
    );
  }

  const http = new HttpRecorder();
  const hub = new HubRecorder();
  const client = createGameClient({
    apiBaseUrl: api,
    fetch: http.fetch,
    connectHub: (hubUrl, options) => hub.wrap(createSignalRConnection(hubUrl, options)),
  });
  const { store } = client;
  const subscribe = (listener: () => void): (() => void) => store.subscribe(listener);
  const phases: PhaseSeen[] = [];
  const unsubscribe = store.subscribe(() => {
    const { roundId, phase } = store.getState();
    const last = phases.at(-1);
    if (last?.roundId !== roundId || last.phase !== phase) phases.push({ roundId, phase });
  });

  try {
    const session = await client.start(launch.launchToken);

    await waitUntil(
      subscribe,
      () => {
        const state = store.getState();
        return state.connection === "connected" && state.phase !== "idle" ? true : undefined;
      },
      30_000,
      "the RoundSnapshot of JoinSala (connection connected, phase not idle)",
    );

    // A round followed from its RoundBetting message, so the whole sequence is observed live.
    const roundId = await waitUntil(
      subscribe,
      () => {
        const state = store.getState();
        return state.phase === "betting" &&
          state.roundId !== null &&
          hub.indexOf("RoundBetting", state.roundId) >= 0
          ? state.roundId
          : undefined;
      },
      roundTimeoutMs,
      "a RoundBetting message reflected in the store",
    );

    await waitUntil(
      subscribe,
      () => {
        const state = store.getState();
        return state.roundId === roundId && state.phase === "crashed" ? true : undefined;
      },
      roundTimeoutMs,
      `the crash of round ${roundId}`,
    );

    return { api, launch, session, http, hub, roundId, phases, finalState: store.getState() };
  } finally {
    unsubscribe();
    await client.stop();
  }
}

function payloadOf(hub: HubRecorder, name: string, roundId: string): Record<string, unknown> {
  const index = hub.indexOf(name, roundId);
  const record = hub.records[index];
  if (record === undefined) throw new Error(`No ${name} recorded for round ${roundId}`);
  return record.payload as Record<string, unknown>;
}

describe("SPEC-0010 game-sdk contra o backend real (Aspire)", () => {
  test(
    "SPEC-0010:IT-01 o game-sdk em Node troca o launchToken, entra na sala e acompanha uma rodada até o crash do servidor",
    async () => {
      const { session, hub, roundId, phases, finalState } = await followLiveRound();

      expect(session.mode).toBe("demo");
      expect(session.salaId).not.toBe("");
      expect(session.nickname).not.toBe("");

      // Snapshot first, then Betting → Flying → Crashed of the same round, in this order.
      const joined = hub.indexOf("JoinSala");
      const betting = hub.indexOf("RoundBetting", roundId);
      const flying = hub.indexOf("RoundFlying", roundId, betting);
      const crashed = hub.indexOf("RoundCrashed", roundId, flying);
      expect(hub.records[joined]?.kind).toBe("result");
      expect(joined).toBeGreaterThanOrEqual(0);
      expect(betting).toBeGreaterThan(joined);
      expect(flying).toBeGreaterThan(betting);
      expect(crashed).toBeGreaterThan(flying);

      // The store went through the same phases for that round.
      expect(
        phases.filter((seen) => seen.roundId === roundId).map((seen) => seen.phase),
      ).toEqual(expect.arrayContaining(["betting", "flying", "crashed"]));
      const order = phases
        .filter((seen) => seen.roundId === roundId)
        .map((seen) => seen.phase)
        .filter((phase, index, all) => all.indexOf(phase) === index);
      expect(order).toEqual(["betting", "flying", "crashed"]);

      // The store ends with the crash point the server sent, never one of its own.
      const crashMessage = payloadOf(hub, "RoundCrashed", roundId);
      expect(finalState.phase).toBe("crashed");
      expect(finalState.roundId).toBe(roundId);
      expect(finalState.crashPoint).toBe(crashMessage["crashPoint"]);
      expect(finalState.revealedSeed).toBe(crashMessage["revealedSeed"]);
      expect(finalState.multiplier).toBe(Number(crashMessage["crashPoint"]));
      expect(finalState.history[0]).toMatchObject({ roundId, crashPoint: crashMessage["crashPoint"] });
      expect(finalState.connection).toBe("connected");
    },
    flowTimeoutMs,
  );

  test(
    "SPEC-0010:CT-02 resultados e mensagens reais do RoundsHub conferem com o contrato gerado do hub (SPEC-0008@2)",
    async () => {
      const [{ hub, roundId }, manifest] = await Promise.all([followLiveRound(), exportHubManifest()]);

      // Every kind the flow must have exercised is present, so the validation below is not vacuous.
      const names = new Set(hub.records.map((record) => record.name));
      for (const name of ["GetServerTime", "JoinSala", "RoundBetting", "RoundFlying", "RoundCrashed"]) {
        expect(names, `the live flow recorded no ${name}`).toContain(name);
      }

      const violations = hub.records.flatMap((record) =>
        schemaErrors(hubSchema(manifest, record.name), record.payload).map(
          (error) => `${record.name} (${record.kind}): ${error}`,
        ),
      );
      expect(violations).toEqual([]);

      // Nothing before the crash carries the crash point or the seed of the followed round (SPEC-0008@2 §6).
      const crashed = hub.indexOf("RoundCrashed", roundId);
      const leaks = hub.records
        .slice(0, crashed)
        .filter((record) => (record.payload as { roundId?: string } | null)?.roundId === roundId)
        .filter((record) => {
          const payload = record.payload as Record<string, unknown>;
          return (payload["crashPoint"] ?? null) !== null || (payload["revealedSeed"] ?? null) !== null;
        })
        .map((record) => record.name);
      expect(leaks).toEqual([]);
    },
    flowTimeoutMs,
  );

  test(
    "SPEC-0010:CT-03 a troca do launchToken por sessão confere com o contrato de lançamento e sessão (SPEC-0009@1)",
    async () => {
      const { api, launch, http } = await followLiveRound();

      const exchanges = http.exchanges.filter(
        (exchange) => exchange.method === "POST" && exchange.url === `${api}/api/v1/sessions`,
      );
      expect(exchanges).toHaveLength(1);
      const exchange = exchanges[0];
      if (exchange === undefined) return;

      expect(exchange.requestBody).toEqual({ launchToken: launch.launchToken });
      expect(
        schemaErrors(
          openApiSchema("/api/v1/sessions", "post", "request", "application/json"),
          exchange.requestBody,
        ),
      ).toEqual([]);
      expect(exchange.status).toBe(200);
      expect(exchange.contentType).toMatch(/^application\/json/u);
      expect(
        schemaErrors(
          openApiSchema("/api/v1/sessions", "post", 200, "application/json"),
          exchange.responseBody,
        ),
      ).toEqual([]);

      // The same launch token again: 401 LAUNCH_TOKEN_USED as ProblemDetails, surfaced as a SessionError.
      const reuse = new HttpRecorder();
      const error = await exchangeLaunchToken(api, launch.launchToken, reuse.fetch).then(
        () => undefined,
        (reason: unknown) => reason,
      );
      expect(error).toBeInstanceOf(SessionError);
      expect((error as SessionError).code).toBe("LAUNCH_TOKEN_USED");
      const rejected = reuse.exchanges[0];
      expect(rejected?.status).toBe(401);
      expect(rejected?.contentType).toMatch(/^application\/problem\+json/u);
      expect(rejected?.responseBody).toMatchObject({ code: "LAUNCH_TOKEN_USED" });
      expect(
        schemaErrors(
          openApiSchema("/api/v1/sessions", "post", 401, "application/problem+json"),
          rejected?.responseBody,
        ),
      ).toEqual([]);
    },
    flowTimeoutMs,
  );
});
