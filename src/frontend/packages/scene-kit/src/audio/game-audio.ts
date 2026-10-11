// The game's only sound entry point (SPEC-0018 §6, ADR-0004, ADR-0024): a sibling of GameAnimator, driven by the same
// RoundState subscriber of the game-sdk. Before unlock() no AudioContext runs and state calls only update the
// internal state (nothing is queued).

export interface GameAudio {
  /** Called in the player's first gesture: creates/resumes the AudioContext and plays only the current state. */
  unlock(): Promise<void>;
  /** Betting phase: light wind loop. After a crash it also plays the calm restart cue. */
  prepare(): void;
  /** Flight started at `startedAt` (epoch ms on the local clock, already corrected by the server offset). */
  takeoff(startedAt: number): void;
  /** Current multiplier while flying (drives wind gain, line tension and milestones). */
  setMultiplier(multiplier: number): void;
  /** Line cut at the server crash point: snap → fall → silence, once. */
  crash(point: number): void;
  /** Back to the initial state (annulled round, resync). */
  reset(): void;
  setMuted(muted: boolean): void;
  /** Master volume in [0, 1]. */
  setVolume(volume: number): void;
  dispose(): void;
}

/**
 * State of the sound for the test hook. "muted" wins over every other state (a muted player is "muted" before and
 * after the first gesture); otherwise "locked" until unlock(), "suspended" while the tab is hidden, "running".
 */
export type AudioState = "locked" | "running" | "suspended" | "muted";

/**
 * Cues of the round script (SPEC-0018 §2):
 * - "wind": the wind loop starts (betting, or the current state at unlock/resume);
 * - "tick": countdown tick of the last 3 s of betting;
 * - "takeoff": the takeoff pull (≤ 600 ms);
 * - "tension": the line tension tone starts (flight, or the current flight at unlock/resume);
 * - "milestone": a multiplier milestone (2x … 100x), once per round, with its pitch;
 * - "snap", "fall", "silence": the crash script (snap ≤ 150 ms, fall until 1500 ms after the crash, then silence);
 * - "restart": the calm restart cue (≤ 600 ms) of the betting phase after a crash.
 */
export type CueName =
  "wind" | "tick" | "takeoff" | "tension" | "milestone" | "snap" | "fall" | "silence" | "restart";

export interface CueEntry {
  readonly name: CueName;
  /** Audio clock time (AudioContext currentTime, ms) at which the cue sounds; may be ahead of currentTime. */
  readonly atMs: number;
  /** Length of a one-shot cue, ms. */
  readonly durationMs?: number;
  /** "milestone": the milestone crossed (2, 5, 10, 25, 50 or 100). */
  readonly milestone?: number;
  /** Pitch of the cue, Hz ("milestone": milestoneHz(i)). */
  readonly hz?: number;
}

/** Audio part of the test hook (test builds only): `window.__APOGEU_TEST__.audio`. */
export interface AudioTestHook {
  /** Cues played since the audio was created (none before unlock() nor while muted). */
  cueLog(): readonly CueEntry[];
  state(): AudioState;
}
