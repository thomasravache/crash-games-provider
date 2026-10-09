import { createRoundStore, type RoundStore } from "@apogeu/game-sdk/state";
import { beforeEach, describe, expect, test } from "vitest";

import {
  bettingEvent,
  bettingRound,
  crashedEvent,
  flyingEvent,
  roundIds,
  snapshotEvent,
  syncEvent,
} from "../support/rounds";

// SPEC-0010 UT-02: out-of-order and duplicated hub messages (SPEC-0008@2) never move the store back once the round
// crashed.

const t0 = Date.parse("2026-10-09T12:00:00.000Z");
const flightMs = 17_000;

function crashedStore(): RoundStore {
  const store = createRoundStore();
  store.dispatch(snapshotEvent(bettingRound(41, t0), [{ ...roundIds(40), crashPoint: "1.52" }]));
  store.dispatch(bettingEvent(41, t0));
  store.dispatch(flyingEvent(41, t0));
  store.dispatch(syncEvent(41, t0 + 10_000, "1.82"));
  store.dispatch(crashedEvent(41, "2.78", t0 + flightMs));
  return store;
}

describe("SPEC-0010 store da rodada com mensagens fora de ordem", () => {
  let store: RoundStore;

  beforeEach(() => {
    store = crashedStore();
  });

  test("SPEC-0010:UT-02 pré-condição: a store está em Crashed com o crash do servidor e a rodada no histórico", () => {
    const state = store.getState();

    expect(state.phase).toBe("crashed");
    expect(state.roundId).toBe("round-41");
    expect(state.crashPoint).toBe("2.78");
    expect(state.multiplier).toBe(2.78);
    expect(state.history[0]).toEqual({ ...roundIds(41), crashPoint: "2.78" });
  });

  test("SPEC-0010:UT-02 um RoundSync atrasado depois do Crashed não muda o estado", () => {
    const before = store.getState();

    store.dispatch(syncEvent(41, t0 + flightMs - 100, "2.75"));

    expect(store.getState()).toStrictEqual(before);
  });

  test("SPEC-0010:UT-02 um RoundCrashed duplicado não muda o estado nem duplica o histórico", () => {
    const before = store.getState();

    store.dispatch(crashedEvent(41, "2.78", t0 + flightMs));

    expect(store.getState()).toStrictEqual(before);
    expect(store.getState().history.filter((entry) => entry.roundId === "round-41")).toHaveLength(
      1,
    );
  });

  test("SPEC-0010:UT-02 um RoundFlying atrasado da mesma rodada não volta o estado para o voo", () => {
    const before = store.getState();

    store.dispatch(flyingEvent(41, t0));

    expect(store.getState()).toStrictEqual(before);
  });
});
