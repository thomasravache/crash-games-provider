import { pauseWhenHidden, type EffectEntry, type VisibilitySource } from "@apogeu/scene-kit";
import { describe, expect, test } from "vitest";

import { flyTo, frameMs, newAnimator, run } from "./support/scene";

// SPEC-0011 UT-08: hidden tab (ADR-0021). The ticker stops while the document is hidden; 10 s later, visible again and
// already in another round, the scene goes straight to the current state without replaying the animations it missed.

class FakeDocument extends EventTarget implements VisibilitySource {
  visibilityState: DocumentVisibilityState = "visible";

  override addEventListener(type: "visibilitychange", listener: () => void): void {
    super.addEventListener(type, listener);
  }

  override removeEventListener(type: "visibilitychange", listener: () => void): void {
    super.removeEventListener(type, listener);
  }

  become(state: DocumentVisibilityState): void {
    this.visibilityState = state;
    this.dispatchEvent(new Event("visibilitychange"));
  }
}

class FakeTicker {
  readonly calls: ("start" | "stop")[] = [];

  start(): void {
    this.calls.push("start");
  }

  stop(): void {
    this.calls.push("stop");
  }
}

const missedNames: readonly EffectEntry["name"][] = ["snap", "flash", "shake", "tumble", "fade", "transition"];

describe("SPEC-0011 aba oculta", () => {
  test("SPEC-0011:UT-08 oculta durante o voo e visível 10 s depois em outra rodada: o ticker pausa e a cena vai direto ao estado atual", () => {
    const document = new FakeDocument();
    const ticker = new FakeTicker();
    const animator = newAnimator("full");
    const stop = pauseWhenHidden(document, ticker, animator);

    animator.prepare();
    run(animator, 500);
    animator.takeoff(0);
    run(animator, 700);
    flyTo(animator, 1, 3);

    document.become("hidden");
    expect(ticker.calls).toEqual(["stop"]);
    const logged = animator.effectsLog().length;
    const pausedAt = animator.frame().timeMs;

    // While hidden the round crashes, the next one bets and takes off; the binding keeps calling the animator.
    animator.crash(3.4);
    animator.prepare();
    animator.takeoff(0);
    animator.setMultiplier(1.2);
    // A ticker that is not stopped by the scene would still deliver frames: they must not move the paused scene.
    animator.advance(frameMs);
    expect(animator.frame().timeMs).toBe(pausedAt);

    document.become("visible");
    expect(ticker.calls).toEqual(["stop", "start"]);
    // The first tick after a long pause may carry the whole gap.
    const back = animator.advance(10_000);
    const next = animator.advance(frameMs);

    expect(back.phase).toBe("flying");
    expect(back.multiplier).toBe(1.2);
    expect(next.phase).toBe("flying");
    expect(next.flashOpacity).toBe(0);
    expect(Math.hypot(next.shake.x, next.shake.y)).toBe(0);
    const replayed = animator
      .effectsLog()
      .slice(logged)
      .map((entry) => entry.name)
      .filter((name) => missedNames.includes(name));
    expect(replayed, "animations of the missed crash and takeoff").toEqual([]);

    stop();
    document.become("hidden");
    expect(ticker.calls).toEqual(["stop", "start"]);
  });
});
