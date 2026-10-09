// Milestones of the multiplier (SPEC-0011 §6): 2x, 5x, 10x, 25x, 50x and 100x fire a pulse once per round, even when
// the multiplier is resynchronized backwards and crosses a milestone again.

export const multiplierMilestones: readonly number[] = [2, 5, 10, 25, 50, 100];

export interface MilestoneTracker {
  /** Milestones crossed for the first time in this round by the new multiplier (ascending; often empty). */
  update(multiplier: number): readonly number[];
  /** New round: every milestone may fire again. */
  reset(): void;
}

export function createMilestoneTracker(): MilestoneTracker {
  // Milestones fire in ascending order, so the ones already fired are always a prefix of the list.
  let fired = 0;
  return {
    update(multiplier) {
      const start = fired;
      while (
        fired < multiplierMilestones.length &&
        multiplier >= (multiplierMilestones[fired] ?? Infinity)
      ) {
        fired += 1;
      }
      return fired === start ? [] : multiplierMilestones.slice(start, fired);
    },
    reset() {
      fired = 0;
    },
  };
}
