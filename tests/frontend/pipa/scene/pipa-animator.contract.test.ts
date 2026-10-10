import type { ScenePhase } from "@apogeu/scene-kit";

import { describeGameAnimatorContract, type ShownState } from "./support/game-animator-contract";
import { newAnimator } from "./support/scene";

// SPEC-0011 CT-01: PipaAnimator passes the GameAnimator contract suite of SPEC-0010@1 unchanged, in both motion
// profiles. The scene phases map to the RoundState phases: takeoff is flying; restart heads back to betting.

const shownPhase: Record<ScenePhase, ShownState["phase"]> = {
  betting: "betting",
  restart: "betting",
  takeoff: "flying",
  flying: "flying",
  crashed: "crashed",
};

for (const profile of ["full", "reduced"] as const) {
  describeGameAnimatorContract(`SPEC-0011:CT-01 [${profile}]`, () => {
    const animator = newAnimator(profile);
    return {
      animator,
      advance(ms) {
        for (let elapsed = 0; elapsed < ms; elapsed += 16) animator.advance(Math.min(16, ms - elapsed));
      },
      shown() {
        const frame = animator.frame();
        return { phase: shownPhase[frame.phase], multiplier: frame.multiplier };
      },
    };
  });
}
