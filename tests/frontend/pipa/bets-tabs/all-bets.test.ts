import { AllBetsTab, betsTabsTestIds } from "@apogeu/game-ui";
import {
  createLiveBetsStore,
  initialLiveBetsState,
  type LiveBetsEvent,
  type LiveBetsState,
} from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import { bettingEvent } from "../support/rounds";
import { render, textOf } from "../bets/support/react";
import {
  liveBatch,
  liveBet,
  liveSnapshot,
  newTabsRig,
  sessionToken,
} from "./support/tabs";

// SPEC-0031 UT-01: a LiveBetsSnapshot (JoinSalaBets) followed by LiveBetsBatch messages (SPEC-0025@4), applied to the
// live bets store, makes "Todas as apostas" show the bets of the round with the anonymized nickname the server sent,
// amount, multiplier and payout of who cashed out, and the round totals; a RoundBetting of another round, from the
// round store, empties the list (Emenda v1).

const t0 = Date.parse("2026-10-15T12:00:00.000Z");

const snapshot41 = liveSnapshot(
  "round-41",
  [liveBet("ref-a", "Ma***", "20.50"), liveBet("ref-b", "Jogador***", "5.00")],
  { totalBets: 2, totalPlayers: 2, totalWagered: "25.50" },
);

const batch41 = liveBatch(
  "round-41",
  [liveBet("ref-c", "Jo***", "10.00")],
  [{ betRef: "ref-a", multiplier: "2.00", payout: "41.00" }],
  { totalBets: 3, totalPlayers: 3, totalWagered: "35.50" },
);

const snapshotEvent = (
  snapshot: typeof snapshot41,
): Extract<LiveBetsEvent, { type: "liveSnapshot" }> => ({
  type: "liveSnapshot",
  ...snapshot,
});

const batchEvent = (
  batch: typeof batch41,
): Extract<LiveBetsEvent, { type: "liveBatch" }> => ({
  type: "liveBatch",
  ...batch,
});

const betRefs = (state: LiveBetsState): string[] =>
  state.bets.map((bet) => bet.betRef).sort();

const row = (markup: string, betRef: string): string =>
  textOf(markup, betsTabsTestIds.liveBet(betRef)) ?? "";

describe("SPEC-0031 aba Todas as apostas", () => {
  test("SPEC-0031:UT-01 snapshot seguido de lote: apostas da rodada com apelido anonimizado, multiplicador e ganho de quem retirou, e totais", () => {
    const store = createLiveBetsStore();
    expect(store.getState()).toEqual(initialLiveBetsState);

    store.dispatch(snapshotEvent(snapshot41));
    expect(store.getState()).toMatchObject({
      roundId: "round-41",
      totalBets: 2,
      totalPlayers: 2,
      totalWagered: "25.50",
    });
    expect(betRefs(store.getState())).toEqual(["ref-a", "ref-b"]);

    store.dispatch(batchEvent(batch41));
    const state = store.getState();
    expect(state).toMatchObject({
      roundId: "round-41",
      totalBets: 3,
      totalPlayers: 3,
      totalWagered: "35.50",
    });
    expect(betRefs(state)).toEqual(["ref-a", "ref-b", "ref-c"]);
    expect(state.bets.find((bet) => bet.betRef === "ref-a")).toMatchObject({
      nickname: "Ma***",
      amount: "20.50",
      multiplier: "2.00",
      payout: "41.00",
    });
    expect(state.bets.find((bet) => bet.betRef === "ref-c")).toMatchObject({
      nickname: "Jo***",
      amount: "10.00",
      multiplier: null,
      payout: null,
    });

    const markup = render(AllBetsTab, { state });
    const cashed = row(markup, "ref-a");
    expect(cashed).toContain("Ma***");
    expect(cashed).toContain("R$ 20,50");
    expect(cashed).toContain("2,00x");
    expect(cashed).toContain("R$ 41,00");
    const open = row(markup, "ref-c");
    expect(open).toContain("Jo***");
    expect(open).toContain("R$ 10,00");
    expect(row(markup, "ref-b")).toContain("Jogador***");
    expect(textOf(markup, betsTabsTestIds.liveTotalBets)).toContain("3");
    expect(textOf(markup, betsTabsTestIds.liveTotalPlayers)).toContain("3");
    expect(textOf(markup, betsTabsTestIds.liveTotalWagered)).toContain(
      "R$ 35,50",
    );
  });

  test("SPEC-0031:UT-01 o cliente entra na sala (JoinSalaBets), aplica os LiveBetsBatch e zera a lista no RoundBetting de outra rodada", async () => {
    const rig = newTabsRig(snapshot41);
    rig.round(bettingEvent(41, t0 + 6_000));
    await rig.client.start(sessionToken);

    // JoinSalaBets has no argument: the server uses the sala of the player's session (SPEC-0025@4).
    expect(rig.hub.calls("JoinSalaBets")).toEqual([[]]);
    expect(rig.hub.listens("LiveBetsBatch")).toBe(true);
    await rig.untilLive((state) => state.roundId === "round-41");
    expect(betRefs(rig.live())).toEqual(["ref-a", "ref-b"]);

    rig.hub.emit("LiveBetsBatch", batch41);
    await rig.untilLive((state) => state.totalBets === 3);
    expect(betRefs(rig.live())).toEqual(["ref-a", "ref-b", "ref-c"]);
    expect(rig.live().bets.find((bet) => bet.betRef === "ref-a")?.payout).toBe(
      "41.00",
    );

    // A new round in betting: the list of round 41 is gone, the totals start over.
    rig.round(bettingEvent(42, t0 + 30_000));
    await rig.untilLive((state) => state.roundId === "round-42");
    expect(rig.live()).toMatchObject({
      roundId: "round-42",
      bets: [],
      totalBets: 0,
      totalPlayers: 0,
    });
    expect(render(AllBetsTab, { state: rig.live() })).not.toContain(
      betsTabsTestIds.liveBet("ref-a"),
    );

    rig.hub.emit(
      "LiveBetsBatch",
      liveBatch("round-42", [liveBet("ref-d", "Ca***", "3.00")], [], {
        totalBets: 1,
        totalPlayers: 1,
        totalWagered: "3.00",
      }),
    );
    await rig.untilLive((state) => state.totalBets === 1);
    expect(betRefs(rig.live())).toEqual(["ref-d"]);
    await rig.client.stop();
  });
});
