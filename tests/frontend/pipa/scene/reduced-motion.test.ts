import {
  crashTimeline,
  motionProfileFrom,
  phaseTransition,
  type EffectEntry,
  type PipaAnimator,
  type SceneFrame,
} from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

import { flyTo, newAnimator, run } from "./support/scene";

// SPEC-0011 UT-05: with MotionProfile=reduced (prefers-reduced-motion, ADR-0020/ADR-0021) takeoff, flight and crash
// have no shake, no flash and no parallax, and the transitions are 300 ms crossfades.

interface Played {
  readonly frames: readonly SceneFrame[];
  readonly log: readonly EffectEntry[];
}

/** Betting → takeoff → flight to 60x → crash → next betting, frame by frame. */
function playTakeoffFlightAndCrash(animator: PipaAnimator): Played {
  animator.prepare();
  const frames = run(animator, 500);
  animator.takeoff(0);
  frames.push(...run(animator, 700));
  frames.push(...flyTo(animator, 1, 60));
  animator.crash(60);
  frames.push(...run(animator, 2_000));
  animator.prepare();
  frames.push(...run(animator, 1_000));
  return { frames, log: animator.effectsLog() };
}

const offsetsOf = (frame: SceneFrame): number[] => frame.clouds.map((layer) => layer.offsetY);

describe("SPEC-0011 movimento reduzido", () => {
  test("SPEC-0011:UT-05 prefers-reduced-motion: reduce dá o perfil reduced; sem a preferência, full", () => {
    const queries: string[] = [];
    const media = (reduce: boolean) => ({
      matchMedia(query: string) {
        queries.push(query);
        return { matches: reduce && query === "(prefers-reduced-motion: reduce)" };
      },
    });

    expect(motionProfileFrom(media(true))).toBe("reduced");
    expect(motionProfileFrom(media(false))).toBe("full");
    expect(motionProfileFrom(undefined)).toBe("full");
    expect(queries).toContain("(prefers-reduced-motion: reduce)");
  });

  test("SPEC-0011:UT-05 perfil reduced: decolagem, voo e crash sem tremor, clarão nem paralaxe", () => {
    const { frames, log } = playTakeoffFlightAndCrash(newAnimator("reduced"));

    expect(frames.length).toBeGreaterThan(0);
    for (const frame of frames) {
      expect(Math.hypot(frame.shake.x, frame.shake.y), `shake at ${String(frame.timeMs)} ms`).toBe(0);
      expect(Math.abs(frame.flashOpacity), `flash at ${String(frame.timeMs)} ms`).toBe(0);
      for (const layer of frame.clouds) {
        expect(Math.abs(layer.parallax), `parallax at ${String(frame.timeMs)} ms`).toBe(0);
      }
    }
    // The cloud layers do not move relative to each other while the camera climbs.
    const first = frames[0];
    if (first === undefined) throw new Error("no frame drawn");
    expect(first.clouds).toHaveLength(3);
    for (const frame of frames) expect(offsetsOf(frame)).toEqual(offsetsOf(first));
    expect(log.map((entry) => entry.name)).not.toContain("shake");
    expect(log.map((entry) => entry.name)).not.toContain("flash");
    expect(log.map((entry) => entry.name)).not.toContain("parallax");
  });

  test("SPEC-0011:UT-05 perfil reduced: as transições de decolagem e de recomeço são esmaecimentos de 300 ms", () => {
    const { log } = playTakeoffFlightAndCrash(newAnimator("reduced"));
    const transitions = log.flatMap((entry) => (entry.name === "transition" ? [entry] : []));

    expect(transitions.map((entry) => entry.transition)).toEqual(
      expect.arrayContaining(["takeoff", "restart"]),
    );
    for (const transition of transitions) {
      expect(transition.style, `${transition.transition} style`).toBe("crossfade");
      expect(transition.durationMs, `${transition.transition} duration`).toBe(300);
    }
    expect(phaseTransition("takeoff", "reduced")).toMatchObject({ style: "crossfade", durationMs: 300 });
    expect(phaseTransition("restart", "reduced")).toMatchObject({ style: "crossfade", durationMs: 300 });
    const crash = crashTimeline("reduced");
    expect(crash.filter((step) => (step.name === "flash" || step.name === "shake") && step.intensity > 0)).toEqual([]);
  });

  test("SPEC-0011:UT-05 controle: no perfil full o mesmo roteiro tem tremor, clarão, paralaxe e decolagem de 600 ms power3.out", () => {
    const { frames, log } = playTakeoffFlightAndCrash(newAnimator("full"));
    const names = log.map((entry) => entry.name);

    expect(names).toEqual(expect.arrayContaining(["shake", "flash", "parallax"]));
    expect(frames.some((frame) => frame.flashOpacity > 0)).toBe(true);
    expect(frames.some((frame) => frame.shake.x !== 0 || frame.shake.y !== 0)).toBe(true);
    expect(frames.some((frame) => frame.clouds.some((layer) => layer.parallax > 0))).toBe(true);
    expect(phaseTransition("takeoff", "full")).toEqual({
      kind: "takeoff",
      style: "tween",
      durationMs: 600,
      ease: "power3.out",
    });
    expect(phaseTransition("restart", "full")).toMatchObject({ durationMs: 600, ease: "sine.inOut" });
  });
});
