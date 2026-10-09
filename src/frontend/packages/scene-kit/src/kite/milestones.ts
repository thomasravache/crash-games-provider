// Milestones of the multiplier (SPEC-0011 §6): 2x, 5x, 10x, 25x, 50x and 100x fire a pulse once per round, even when
// the multiplier is resynchronized backwards and crosses a milestone again.

import { notImplemented } from "./not-implemented";

export interface MilestoneTracker {
  /** Milestones crossed for the first time in this round by the new multiplier (ascending; often empty). */
  update(multiplier: number): readonly number[];
  /** New round: every milestone may fire again. */
  reset(): void;
}

export function createMilestoneTracker(): MilestoneTracker {
  return notImplemented();
}
