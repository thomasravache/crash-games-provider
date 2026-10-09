// Contract suite of GameAnimator (SPEC-0010@1 §6: prepare(), takeoff(startedAt), setMultiplier(m), crash(point),
// reset()), run against any implementation (SPEC-0011 CT-01 runs it against PipaAnimator). An implementation is
// driven by the state sequences of SPEC-0010 — directly and through bindGameAnimator with the round store — and must
// show the phase and multiplier it was told, tolerate the resync cases and never throw.
import { createRoundStore } from "@apogeu/game-sdk/state";
import { bindGameAnimator, type GameAnimator } from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

import {
  bettingEvent,
  crashedEvent,
  flyingEvent,
  roundIds,
  snapshotEvent,
  syncEvent,
} from "../../support/rounds";

/** What the scene shows, in the phases of the RoundState of SPEC-0010@1. */
export interface ShownState {
  readonly phase: "betting" | "flying" | "crashed";
  readonly multiplier: number;
}

export interface AnimatorUnderContract {
  readonly animator: GameAnimator;
  /** Lets `ms` of scene time pass (the renderer's ticker). */
  advance(ms: number): void;
  shown(): ShownState;
}

const t0 = Date.parse("2026-10-09T12:00:00.000Z");

export function describeGameAnimatorContract(tag: string, create: () => AnimatorUnderContract): void {
  describe(`${tag} contrato do GameAnimator (SPEC-0010@1)`, () => {
    test(`${tag} sequência canônica: prepare → takeoff → setMultiplier → crash → reset`, () => {
      const { animator, advance, shown } = create();

      animator.prepare();
      advance(1_000);
      expect(shown()).toEqual({ phase: "betting", multiplier: 1 });

      animator.takeoff(t0);
      advance(700);
      expect(shown().phase).toBe("flying");
      for (const multiplier of [1.06, 1.34, 2.45]) {
        animator.setMultiplier(multiplier);
        advance(16);
      }
      expect(shown()).toEqual({ phase: "flying", multiplier: 2.45 });

      animator.crash(2.78);
      advance(16);
      expect(shown()).toEqual({ phase: "crashed", multiplier: 2.78 });
      // A late tick after the crash does not move the scene back into flight.
      animator.setMultiplier(2.9);
      advance(2_000);
      expect(shown()).toEqual({ phase: "crashed", multiplier: 2.78 });

      animator.reset();
      advance(1_000);
      expect(shown()).toEqual({ phase: "betting", multiplier: 1 });
    });

    test(`${tag} rodadas pelo adaptador bindGameAnimator: Betting → Flying → Crashed → Betting → anulada`, () => {
      const { animator, advance, shown } = create();
      const store = createRoundStore();
      store.dispatch(snapshotEvent(null));
      const unbind = bindGameAnimator(store, animator);

      store.dispatch(bettingEvent(7, t0));
      advance(1_000);
      expect(shown()).toEqual({ phase: "betting", multiplier: 1 });

      store.dispatch(flyingEvent(7, t0));
      for (const elapsedMs of [1_000, 5_000, 15_000]) {
        store.tick(t0 + elapsedMs);
        advance(16);
      }
      advance(700);
      expect(shown().phase).toBe("flying");
      expect(shown().multiplier).toBe(store.getState().multiplier);

      store.dispatch(crashedEvent(7, "2.78", t0 + 17_040));
      advance(2_000);
      expect(shown()).toEqual({ phase: "crashed", multiplier: 2.78 });

      store.dispatch(bettingEvent(8, t0 + 30_000));
      advance(1_000);
      expect(shown()).toEqual({ phase: "betting", multiplier: 1 });

      store.dispatch({ type: "annulled", roundId: roundIds(8).roundId, reason: "test" });
      advance(1_000);
      expect(shown()).toEqual({ phase: "betting", multiplier: 1 });
      unbind();
    });

    test(`${tag} ressincronização: entrada no meio do voo, multiplicador para trás, crash sem decolagem e prepare repetido`, () => {
      const { animator, advance, shown } = create();

      // Joined mid-flight (snapshot of a flying round): takeoff and the current multiplier, without prepare.
      animator.takeoff(t0 - 40_000);
      animator.setMultiplier(11);
      advance(700);
      expect(shown()).toEqual({ phase: "flying", multiplier: 11 });

      // A sync takes the interpolated multiplier slightly back (12x → 11.8x).
      animator.setMultiplier(12);
      advance(16);
      animator.setMultiplier(11.8);
      advance(16);
      expect(shown()).toEqual({ phase: "flying", multiplier: 11.8 });

      // A takeoff repeated by a reconnection keeps the flight.
      animator.takeoff(t0 - 40_000);
      animator.setMultiplier(12.1);
      advance(16);
      expect(shown()).toEqual({ phase: "flying", multiplier: 12.1 });

      animator.prepare();
      animator.prepare();
      advance(1_000);
      expect(shown()).toEqual({ phase: "betting", multiplier: 1 });

      // Joined at a crashed round: crash without a takeoff.
      animator.crash(4.5);
      advance(2_000);
      expect(shown()).toEqual({ phase: "crashed", multiplier: 4.5 });

      animator.reset();
      animator.reset();
      advance(1_000);
      expect(shown()).toEqual({ phase: "betting", multiplier: 1 });
    });

    test(`${tag} estado de sync do servidor durante o voo é seguido sem exceção`, () => {
      const { animator, advance, shown } = create();
      const store = createRoundStore();
      store.dispatch(snapshotEvent(null));
      const unbind = bindGameAnimator(store, animator);

      store.dispatch(bettingEvent(9, t0));
      store.dispatch(flyingEvent(9, t0));
      store.dispatch(syncEvent(9, t0 + 5_000, "1.35"));
      advance(700);
      expect(shown().phase).toBe("flying");
      expect(Number.isFinite(shown().multiplier)).toBe(true);
      unbind();
    });
  });
}
