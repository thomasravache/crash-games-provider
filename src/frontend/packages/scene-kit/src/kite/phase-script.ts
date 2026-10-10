// GSAP timelines of the phases (SPEC-0011 §4 Timeline, ADR-0021): built from the data of `timeline.ts`, always paused
// and positioned by the scene clock (`seek`), so the script follows the scene time — frozen by the test hook, stopped
// with the tab hidden — and never the GSAP ticker.

import { gsap } from "gsap";

import type { MotionProfile } from "./motion-profile";
import { crashTimeline, phaseTransition, type CrashStep, type TransitionKind } from "./timeline";

type GsapTimeline = ReturnType<typeof gsap.timeline>;

/** Values the scripts animate, all 0..1 except `flash` (opacity) and `shake` (px). */
export interface ScriptValues {
  /** Takeoff: 0 on the ground → 1 in flight. */
  lift: number;
  /** Restart: 0 at the crashed scene → 1 at the initial scene. */
  restart: number;
  flash: number;
  shake: number;
  /** Tumble of the falling kite: 0 → 1. */
  tumble: number;
  /** Visibility of the cut kite and line: 1 → 0. */
  fade: number;
}

export interface PhaseScript {
  readonly values: Readonly<ScriptValues>;
  /** Positions the script `ms` after its start. */
  seek(ms: number): void;
  readonly durationMs: number;
  kill(): void;
}

const restingValues = (): ScriptValues => ({
  lift: 1,
  restart: 1,
  flash: 0,
  shake: 0,
  tumble: 0,
  fade: 1,
});

function wrap(
  values: ScriptValues,
  timeline: GsapTimeline | null,
  durationMs: number,
): PhaseScript {
  return {
    values,
    durationMs,
    seek(ms) {
      timeline?.time(Math.max(0, ms) / 1000, true);
    },
    kill() {
      timeline?.kill();
    },
  };
}

function newTimeline(): GsapTimeline {
  // Paused and outside the global timeline: only `seek` moves it.
  const timeline = gsap.timeline({ paused: true });
  gsap.globalTimeline.remove(timeline);
  return timeline;
}

/** Nothing to animate: the scene at rest (betting, flight). */
export function restingScript(): PhaseScript {
  return wrap(restingValues(), null, 0);
}

/** Takeoff or restart transition. */
export function transitionScript(kind: TransitionKind, profile: MotionProfile): PhaseScript {
  const transition = phaseTransition(kind, profile);
  const values = { ...restingValues(), [kind === "takeoff" ? "lift" : "restart"]: 0 };
  const timeline = newTimeline();
  timeline.to(
    values,
    {
      [kind === "takeoff" ? "lift" : "restart"]: 1,
      duration: transition.durationMs / 1000,
      ease: transition.ease,
    },
    0,
  );
  return wrap(values, timeline, transition.durationMs);
}

const stepSeconds = (step: CrashStep): number => (step.endMs - step.startMs) / 1000;

/** The crash: snap → flash → shake → tumble → fade. */
export function crashScript(profile: MotionProfile): PhaseScript {
  const values = restingValues();
  const timeline = newTimeline();
  const steps = crashTimeline(profile);
  for (const step of steps) {
    const at = step.startMs / 1000;
    switch (step.name) {
      case "snap":
        break;
      case "flash":
        timeline.fromTo(
          values,
          { flash: step.intensity },
          { flash: 0, duration: stepSeconds(step), ease: "power2.in" },
          at,
        );
        break;
      case "shake":
        timeline.fromTo(
          values,
          { shake: step.intensity },
          { shake: 0, duration: stepSeconds(step), ease: "power2.out" },
          at,
        );
        break;
      case "tumble":
        timeline.fromTo(
          values,
          { tumble: 0 },
          { tumble: 1, duration: stepSeconds(step), ease: "none" },
          at,
        );
        break;
      case "fade":
        timeline.fromTo(
          values,
          { fade: 1 },
          { fade: 0, duration: stepSeconds(step), ease: "sine.in" },
          at,
        );
        break;
    }
  }
  timeline.time(0, true);
  return wrap(values, timeline, Math.max(...steps.map((step) => step.endMs)));
}
