// The scene's only animation entry point (ADR-0004, ADR-0021): code animation today, Spine later, swapping only the
// implementation of this interface.

export interface GameAnimator {
  /** Betting phase: kite on the ground, line slack. */
  prepare(): void;
  /** Flight started at `startedAt` (epoch ms on the local clock, already corrected by the server offset). */
  takeoff(startedAt: number): void;
  /** Current multiplier while flying (drives height and sky). */
  setMultiplier(multiplier: number): void;
  /** Line cut at the server crash point: kite spins away. */
  crash(point: number): void;
  /** Back to the initial scene (annulled round, resync). */
  reset(): void;
}
