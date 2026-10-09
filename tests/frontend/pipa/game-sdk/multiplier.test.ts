import { createRoundStore, estimateServerOffset, multiplierAt } from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import { bettingEvent, bettingRound, flyingEvent, iso, snapshotEvent } from "../support/rounds";

// SPEC-0010 UT-01: client interpolation m(t) = e^(k·t) on the server clock (SPEC-0008@2 §4), with the local clock
// 120 ms ahead of the server. Expected values are floor(e^(0.00006·t)·100)/100 computed independently (Python), at
// instants where ignoring the skew would show a different value.

const k = 0.00006;
const skewMs = 120; // local clock − server clock
const startedAtMs = Date.parse("2026-10-09T12:00:00.000Z");

/** Local instant at which `elapsedMs` of flight have passed on the server clock. */
const localAt = (elapsedMs: number): number => startedAtMs + elapsedMs + skewMs;

const expected: readonly (readonly [elapsedMs: number, shown: number])[] = [
  [0, 1.0],
  [1_000, 1.06],
  [5_000, 1.34],
  [11_552, 1.99],
  [15_000, 2.45],
  [30_000, 6.04],
  [60_000, 36.59],
];

describe("SPEC-0010 interpolação do multiplicador", () => {
  test("SPEC-0010:UT-01 o desvio de relógio estimado pelo GetServerTime é servidor − local (−120 ms)", () => {
    const local = localAt(2_000);

    expect(
      estimateServerOffset({
        requestSentAtMs: local,
        responseReceivedAtMs: local,
        serverTime: iso(local - skewMs),
      }),
    ).toBe(-skewMs);
    // 40 ms round trip: the server answered at the midpoint.
    expect(
      estimateServerOffset({
        requestSentAtMs: local,
        responseReceivedAtMs: local + 40,
        serverTime: iso(local + 20 - skewMs),
      }),
    ).toBe(-skewMs);
  });

  test.each(expected)(
    "SPEC-0010:UT-01 em t local o multiplicador é m(t − desvio) do servidor com 2 casas (voo %i ms → %f)",
    (elapsedMs, shown) => {
      const value = multiplierAt(
        { startedAtMs, growthK: k, serverOffsetMs: -skewMs },
        localAt(elapsedMs),
      );

      expect(value).toBeCloseTo(shown, 2);
      expect(Math.round(value * 100)).toBe(Math.round(shown * 100));
    },
  );

  test("SPEC-0010:UT-01 antes do início do voo no relógio do servidor o multiplicador é 1,00", () => {
    // Local clock already past startedAt, server clock not yet.
    expect(
      multiplierAt({ startedAtMs, growthK: k, serverOffsetMs: -skewMs }, startedAtMs + 60),
    ).toBe(1);
  });

  test("SPEC-0010:UT-01 a store interpola com o desvio de relógio registrado", () => {
    const store = createRoundStore();
    store.setServerOffset(-skewMs);
    store.dispatch(snapshotEvent(bettingRound(7, startedAtMs)));
    store.dispatch(bettingEvent(7, startedAtMs));
    store.dispatch(flyingEvent(7, startedAtMs));

    store.tick(localAt(15_000));

    expect(store.getState().phase).toBe("flying");
    expect(store.getState().serverOffsetMs).toBe(-skewMs);
    expect(store.getState().multiplier).toBeCloseTo(2.45, 2);
  });
});
