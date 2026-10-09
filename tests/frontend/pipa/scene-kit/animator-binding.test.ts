import { createRoundStore } from "@apogeu/game-sdk/state";
import { bindGameAnimator, type GameAnimator } from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

import { bettingEvent, crashedEvent, flyingEvent, snapshotEvent } from "../support/rounds";

// SPEC-0010 UT-06: the scene is driven only through GameAnimator (ADR-0004). A fake animator records the calls while
// the store goes Betting → Flying → Crashed.

type Call =
  | { readonly name: "prepare" | "reset" }
  | { readonly name: "takeoff"; readonly startedAt: number }
  | { readonly name: "setMultiplier"; readonly multiplier: number }
  | { readonly name: "crash"; readonly point: number };

class FakeAnimator implements GameAnimator {
  readonly calls: Call[] = [];

  prepare(): void {
    this.calls.push({ name: "prepare" });
  }

  takeoff(startedAt: number): void {
    this.calls.push({ name: "takeoff", startedAt });
  }

  setMultiplier(multiplier: number): void {
    this.calls.push({ name: "setMultiplier", multiplier });
  }

  crash(point: number): void {
    this.calls.push({ name: "crash", point });
  }

  reset(): void {
    this.calls.push({ name: "reset" });
  }
}

/** Names with consecutive repetitions collapsed: ["setMultiplier", "setMultiplier"] → ["setMultiplier"]. */
const collapsed = (calls: readonly Call[]): string[] =>
  calls.map((call) => call.name).filter((name, index, names) => names[index - 1] !== name);

const t0 = Date.parse("2026-10-09T12:00:00.000Z");

describe("SPEC-0010 animações pelo adaptador GameAnimator", () => {
  test("SPEC-0010:UT-06 Betting → Flying → Crashed chama prepare, takeoff, setMultiplier (n vezes) e crash, nessa ordem", () => {
    const store = createRoundStore();
    const animator = new FakeAnimator();
    store.dispatch(snapshotEvent(null));
    const unbind = bindGameAnimator(store, animator);

    store.dispatch(bettingEvent(7, t0));
    store.dispatch(flyingEvent(7, t0));
    for (const elapsedMs of [1_000, 5_000, 15_000]) {
      store.tick(t0 + elapsedMs);
    }
    store.dispatch(crashedEvent(7, "2.78", t0 + 17_040));

    expect(collapsed(animator.calls)).toEqual(["prepare", "takeoff", "setMultiplier", "crash"]);
    expect(animator.calls).toContainEqual({ name: "takeoff", startedAt: t0 });
    const multipliers = animator.calls.flatMap((call) =>
      call.name === "setMultiplier" ? [call.multiplier] : [],
    );
    expect(multipliers.length).toBeGreaterThanOrEqual(3);
    expect(multipliers).toEqual([...multipliers].sort((a, b) => a - b));
    expect(multipliers).toEqual(expect.arrayContaining([1.06, 1.34, 2.45]));
    expect(animator.calls.at(-1)).toEqual({ name: "crash", point: 2.78 });

    const callsBefore = animator.calls.length;
    unbind();
    store.dispatch(bettingEvent(8, t0 + 30_000));
    expect(animator.calls).toHaveLength(callsBefore);
  });
});
