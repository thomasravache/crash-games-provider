// CueScheduler (SPEC-0018 §4): the milestone cues 2x, 5x, 10x, 25x, 50x and 100x, once per round even when a resync
// takes the multiplier back below a milestone already played, each one a tone higher than the previous. The same
// milestones as the scene's pulses (SPEC-0011), from its MilestoneTracker.

import { createMilestoneTracker, multiplierMilestones } from "../kite/milestones";
import { milestoneHz } from "./audio-mix";

export interface MilestoneCue {
  /** The milestone crossed (2, 5, 10, 25, 50 or 100). */
  readonly milestone: number;
  /** Position of the milestone in the list (0 for 2x … 5 for 100x). */
  readonly index: number;
  /** milestoneHz(index). */
  readonly hz: number;
}

export interface CueScheduler {
  /** Milestone cues crossed for the first time in this round by the new multiplier (ascending; often empty). */
  update(multiplier: number): readonly MilestoneCue[];
  /** New round: every milestone may sound again. */
  reset(): void;
}

export function createCueScheduler(): CueScheduler {
  const tracker = createMilestoneTracker();
  return {
    update(multiplier) {
      return tracker.update(multiplier).map((milestone) => {
        const index = multiplierMilestones.indexOf(milestone);
        return { milestone, index, hz: milestoneHz(index) };
      });
    },
    reset() {
      tracker.reset();
    },
  };
}
