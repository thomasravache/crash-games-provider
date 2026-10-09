import { createGameClient, type GameClient } from "@apogeu/game-sdk";
import { afterEach, describe, expect, test, vi } from "vitest";

import { FakeRoundsHub, fakeSessionFetch } from "../support/fake-hub";
import {
  iso,
  multiplier,
  roundIds,
  salaId,
  sessionResponse,
  wireSnapshot,
} from "../support/rounds";

// SPEC-0010 UT-03: after a reconnection the client joins the sala again and the store is replaced by the fresh
// RoundSnapshot (SPEC-0008@2), with `connection` back to `connected`.

const apiBaseUrl = "http://api.test";
const t0 = Date.parse("2026-10-09T12:00:00.000Z");

describe("SPEC-0010 reconexão do cliente do jogo", () => {
  let client: GameClient | undefined;

  afterEach(async () => {
    await client?.stop();
    client = undefined;
  });

  test("SPEC-0010:UT-03 numa reconexão o RoundSnapshot substitui a store e connection volta a connected", async () => {
    let now = t0;
    const hub = new FakeRoundsHub(() => now);
    const session = fakeSessionFetch(apiBaseUrl, sessionResponse);
    // Before the drop: round 41 flying.
    hub.queueSnapshot(
      wireSnapshot(
        {
          ...roundIds(41),
          state: "flying",
          commitment: "aa".repeat(32),
          bettingEndsAt: iso(t0 - 1_000),
          startedAt: iso(t0 - 1_000),
          crashPoint: null,
          crashedAt: null,
          revealedSeed: null,
        },
        [{ roundNumber: 40, crashPoint: "1.52" }],
      ),
    );
    // After the return: round 41 crashed while offline and round 42 is betting.
    hub.queueSnapshot(
      wireSnapshot(
        {
          ...roundIds(42),
          state: "betting",
          commitment: "bb".repeat(32),
          bettingEndsAt: iso(t0 + 30_000),
          startedAt: null,
          crashPoint: null,
          crashedAt: null,
          revealedSeed: null,
        },
        [
          { roundNumber: 41, crashPoint: "3.12" },
          { roundNumber: 40, crashPoint: "1.52" },
        ],
      ),
    );

    client = createGameClient({
      apiBaseUrl,
      fetch: session.fetch,
      connectHub: (hubUrl, options) => {
        hub.hubUrl = hubUrl;
        hub.options = options;
        return hub;
      },
      now: () => now,
    });
    await client.start("launch-token-of-the-test");
    const store = client.store;

    expect(session.requests[0]?.url).toBe(`${apiBaseUrl}/api/v1/sessions`);
    expect(JSON.parse(String(session.requests[0]?.init?.body))).toEqual({
      launchToken: "launch-token-of-the-test",
    });
    expect(hub.hubUrl).toBe(`${apiBaseUrl}/hubs/rounds`);
    expect(hub.options?.accessTokenFactory()).toBe(sessionResponse.sessionToken);
    expect(store.getState()).toMatchObject({
      phase: "flying",
      roundId: "round-41",
      connection: "connected",
    });

    hub.emit("RoundSync", {
      roundId: "round-41",
      serverTime: iso(t0),
      multiplier: multiplier("1.06"),
    });
    hub.dropConnection();
    expect(store.getState().connection).toBe("reconnecting");

    now = t0 + 20_000;
    hub.restoreConnection();

    await vi.waitFor(() => {
      expect(store.getState().connection).toBe("connected");
      expect(store.getState().roundId).toBe("round-42");
    });
    expect(store.getState()).toMatchObject({
      phase: "betting",
      roundId: "round-42",
      roundNumber: 42,
      commitment: "bb".repeat(32),
      bettingEndsAt: iso(t0 + 30_000),
      startedAt: null,
      crashPoint: null,
      revealedSeed: null,
      multiplier: 1,
      connection: "connected",
    });
    expect(store.getState().history).toEqual([
      { ...roundIds(41), crashPoint: "3.12" },
      { ...roundIds(40), crashPoint: "1.52" },
    ]);
    expect(hub.joinSalaCalls()).toEqual([[salaId], [salaId]]);
  });
});
