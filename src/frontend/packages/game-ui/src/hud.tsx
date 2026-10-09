// HUD from the Stitch screen "Pipa Crash - Jogo da Pipa" (SPEC-0010 §6): history, multiplier, phase banner, DEMO
// badge, fixed demo balance, disabled bet panel ("Apostas em breve"), "Comprovadamente Justo" entry and an aria-live
// region announcing only the phase changes (ADR-0020).

import type { RoundState } from "@apogeu/game-sdk/state";
import type { JSX } from "react";

import { NotImplementedError } from "./not-implemented";

export interface HudProps {
  readonly state: RoundState;
  /** Player nickname (the only personal data shown). */
  readonly nickname: string;
  /** Fixed demo balance, formatted decimal string. */
  readonly demoBalance: string;
  readonly onOpenFairness: () => void;
}

export function Hud(props: HudProps): JSX.Element {
  throw new NotImplementedError("Hud", props);
}
