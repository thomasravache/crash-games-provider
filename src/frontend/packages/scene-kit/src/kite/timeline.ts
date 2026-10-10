// Timeline of SPEC-0011 §2/§6: the script of each phase, as data the GSAP timelines are built from.
// Crash: snap (0 ms) → flash (0–150 ms, opacity ≤ 0.25) → shake (0–300 ms, ≤ 8 px) → tumble (300–1800 ms) → fade.
// Takeoff: 600 ms power3.out; restart: 600 ms sine.inOut; reduced MotionProfile: no shake nor flash, transitions are
// 300 ms crossfades.

import type { MotionProfile } from "./motion-profile";

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

/** Red flash of the crash: rgba(239, 68, 68, 0.25) of the design, at most 25% opaque (WCAG 2.3.1). */
export const crashFlashOpacity = 0.25;
/** Screen shake of the crash, px. */
export const crashShakePx = 8;
/** Crossfade of every transition with the reduced MotionProfile, ms. */
export const crossfadeMs = 300;

const fullCrash: readonly CrashStep[] = [
  { name: "snap", startMs: 0, endMs: 0, intensity: 1 },
  { name: "flash", startMs: 0, endMs: 120, intensity: crashFlashOpacity },
  { name: "shake", startMs: 0, endMs: 300, intensity: crashShakePx },
  { name: "tumble", startMs: 300, endMs: 1_800, intensity: 1 },
  { name: "fade", startMs: 1_800, endMs: 2_300, intensity: 1 },
];

// Reduced: the line is cut and the kite crossfades out; no flash, shake nor tumble.
const reducedCrash: readonly CrashStep[] = [
  { name: "snap", startMs: 0, endMs: 0, intensity: 1 },
  { name: "fade", startMs: 0, endMs: crossfadeMs, intensity: 1 },
];

/** The crash script, in order. */
export function crashTimeline(profile: MotionProfile): readonly CrashStep[] {
  return profile === "reduced" ? reducedCrash : fullCrash;
}

/** Time after crash() when the crash script is over, ms. */
export function crashScriptEndMs(profile: MotionProfile): number {
  return Math.max(...crashTimeline(profile).map((step) => step.endMs));
}

export function phaseTransition(kind: TransitionKind, profile: MotionProfile): Transition {
  if (profile === "reduced")
    return { kind, style: "crossfade", durationMs: crossfadeMs, ease: "none" };
  return kind === "takeoff"
    ? { kind, style: "tween", durationMs: 600, ease: "power3.out" }
    : { kind, style: "tween", durationMs: 600, ease: "sine.inOut" };
}
