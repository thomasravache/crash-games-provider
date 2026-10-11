// The Pipa bet area (SPEC-0026): the two panels (APOSTA 1 / APOSTA 2) and the confirmed balance with "Recarregar demo",
// bound to the BetsClient of game-sdk. Each panel keeps its own draft (amount, automatic cash-out) and only sends
// intents; what it shows comes from the bets store.

import type { BetsClient } from "@apogeu/game-sdk";
import type { RoundState } from "@apogeu/game-sdk/state";
import type { JSX } from "react";

import { notImplemented } from "./not-implemented";

export interface PipaBetsProps {
  readonly client: BetsClient;
  /** Round state (phase and current multiplier) the panels follow. */
  readonly round: RoundState;
}

/** APOSTA 1 and APOSTA 2, side by side (slot `bets` of the Hud). */
export function PipaBets(props: PipaBetsProps): JSX.Element {
  return notImplemented(props);
}

export interface PipaBalanceProps {
  readonly client: BetsClient;
}

/** "R$ 2.990,00 · DEMO" and "Recarregar demo" (slot `balance` of the Hud). */
export function PipaBalance(props: PipaBalanceProps): JSX.Element {
  return notImplemented(props);
}
