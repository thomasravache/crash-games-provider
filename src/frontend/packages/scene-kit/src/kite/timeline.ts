// Timeline of SPEC-0011 §2/§6: the script of each phase, as data the GSAP timelines are built from.
// Crash: snap (0 ms) → flash (0–150 ms, opacity ≤ 0.25) → shake (0–300 ms, ≤ 8 px) → tumble (300–1800 ms) → fade.
// Takeoff: 600 ms power3.out; restart: 600 ms sine.inOut; reduced MotionProfile: no shake nor flash, transitions are
// 300 ms crossfades.

import type { MotionProfile } from "./motion-profile";
import { notImplemented } from "./not-implemented";

export type CrashStepName = "snap" | "flash" | "shake" | "tumble" | "fade";

export interface CrashStep {
  readonly name: CrashStepName;
  /** Start, ms after crash(). */
  readonly startMs: number;
  /** End, ms after crash() (equal to the start for an instant step). */
  readonly endMs: number;
  /** Peak opacity of the flash; amplitude (px) of the shake; 1 for the other steps. */
  readonly intensity: number;
}

export type TransitionKind = "takeoff" | "restart";

export type TransitionStyle = "tween" | "crossfade";

export interface Transition {
  readonly kind: TransitionKind;
  readonly style: TransitionStyle;
  readonly durationMs: number;
  /** GSAP ease, e.g. "power3.out". */
  readonly ease: string;
}

/** The crash script, in order. */
export function crashTimeline(profile: MotionProfile): readonly CrashStep[] {
  return notImplemented(profile);
}

export function phaseTransition(kind: TransitionKind, profile: MotionProfile): Transition {
  return notImplemented(kind, profile);
}
