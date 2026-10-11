// The real bets client of game-sdk (SPEC-0026) over the doubles: fake BetsHub, fake wallet fetch and a real round
// store driven by the RoundsHub fixtures of SPEC-0010. What the client sends and the store shows are read back.
import { createBetsClient, type BetsClient } from "@apogeu/game-sdk";
import {
  createRoundStore,
  type BetsState,
  type RoundEvent,
  type RoundStore,
} from "@apogeu/game-sdk/state";
import { vi } from "vitest";

import { apiBaseUrl, limits, sessionToken } from "./bets";
import { FakeBetsHub } from "./fake-bets-hub";
import { FakeWallet } from "./fake-wallet";

export interface BetsRig {
  readonly client: BetsClient;
  readonly hub: FakeBetsHub;
  readonly wallet: FakeWallet;
  readonly rounds: RoundStore;
  state(): BetsState;
  /** The round store receives a RoundsHub event (the client follows it). */
  round(event: RoundEvent): void;
  /** Waits until `probe` holds on the bets store (async hub/fetch answers). */
  until(probe: (state: BetsState) => boolean): Promise<void>;
}

export function newBetsRig(balance = "3000.00"): BetsRig {
  const hub = new FakeBetsHub(limits);
  const wallet = new FakeWallet(balance);
  const rounds = createRoundStore();
  let nextId = 0;
  const client = createBetsClient({
    apiBaseUrl,
    rounds,
    fetch: wallet.fetch,
    connectHub: (hubUrl, options) => {
      hub.hubUrl = hubUrl;
      hub.options = options;
      return hub;
    },
    newClientBetId: () => {
      nextId += 1;
      return `client-bet-${String(nextId)}`;
    },
  });
  return {
    client,
    hub,
    wallet,
    rounds,
    state: () => client.store.getState(),
    round: (event) => {
      rounds.dispatch(event);
    },
    until: async (probe) => {
      await vi.waitFor(
        () => {
          if (!probe(client.store.getState()))
            throw new Error("bets store not there yet");
        },
        { timeout: 1_000, interval: 5 },
      );
    },
  };
}

/** Starts the client with the session token of the fixtures. */
export async function startedRig(balance = "3000.00"): Promise<BetsRig> {
  const rig = newBetsRig(balance);
  await rig.client.start(sessionToken);
  return rig;
}
