// Effects of SPEC-0011 §4/§5: flash, shake, particles and filters with fixed ceilings (≤ 300 particles, ≤ 3 active
// filters, pixel density ≤ 2), and the log of the effects the scene played (read by the tests through the test hook).

import type { TransitionKind, TransitionStyle } from "./timeline";
import { notImplemented } from "./not-implemented";

/** An effect the scene started, at `atMs` of the scene clock. */
export type EffectEntry =
  | {
      readonly name: "transition";
      readonly atMs: number;
      readonly transition: TransitionKind;
      readonly style: TransitionStyle;
      readonly durationMs: number;
    }
  | { readonly name: "pulse"; readonly atMs: number; readonly milestone: number }
  | {
      readonly name: "flash";
      readonly atMs: number;
      readonly durationMs: number;
      readonly opacity: number;
    }
  | {
      readonly name: "shake";
      readonly atMs: number;
      readonly durationMs: number;
      readonly amplitudePx: number;
    }
  | { readonly name: "parallax"; readonly atMs: number }
  | {
      readonly name: "snap" | "tumble" | "fade";
      readonly atMs: number;
      readonly durationMs: number;
    };

export type EffectName = EffectEntry["name"];

/** Resolution the renderer uses for the device pixel ratio (never above 2). */
export function sceneResolution(devicePixelRatio: number): number {
  return notImplemented(devicePixelRatio);
}
