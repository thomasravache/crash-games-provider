// Helpers of the SPEC-0018 unit and contract tests: a PipaAudio driven over the hand-written OfflineAudioContext double
// (fake-audio-context.ts), a 60 fps "ticker" made of audio clock steps, in-memory storages and a visibility source.
import type { VisibilitySource } from "@apogeu/scene-kit";

import {
  createPipaAudio,
  type CueEntry,
  type CueName,
  type PipaAudio,
  type PipaAudioOptions,
  type PreferenceStorage,
} from "./audio-module";
import { FakeAudioContext, type FakeSourceNode } from "./fake-audio-context";

/** One frame at 60 fps, ms. */
export const frameMs = 1000 / 60;

/** 6 dB as an amplitude ratio (10^(6/20) ≈ 1.995). */
export const sixDb = 10 ** (6 / 20);

export interface AudioRig {
  readonly audio: PipaAudio;
  /** Contexts created by the engine (at most one is expected). */
  readonly contexts: readonly FakeAudioContext[];
  /** The context the engine created; throws when it created none. */
  context(): FakeAudioContext;
  /** Lets `ms` of audio time pass, in 60 fps frames (nothing moves before a context exists or while it is suspended). */
  advance(ms: number): void;
  /** Audio clock now, ms (0 before a context exists). */
  nowMs(): number;
}

export function newAudioRig(options: Omit<PipaAudioOptions, "createContext"> = {}): AudioRig {
  const contexts: FakeAudioContext[] = [];
  const audio = createPipaAudio({
    storage: null,
    visibility: null,
    ...options,
    createContext: () => {
      const context = new FakeAudioContext();
      contexts.push(context);
      return context;
    },
  });
  const context = (): FakeAudioContext => {
    const created = contexts.at(-1);
    if (created === undefined) throw new Error("the engine created no AudioContext");
    return created;
  };
  return {
    audio,
    contexts,
    context,
    advance(ms) {
      for (let elapsed = 0; elapsed < ms; elapsed += frameMs) contexts.at(-1)?.advance(Math.min(frameMs, ms - elapsed));
    },
    nowMs: () => (contexts.at(-1)?.currentTime ?? 0) * 1000,
  };
}

/**
 * Flight from `from` up to `target`, growing 2% per frame as the interpolated multiplier of the store does (one
 * setMultiplier and one frame at a time).
 */
export function flyTo(rig: AudioRig, from: number, target: number): void {
  for (let value = from; value < target; value = Math.min(target, value * 1.02)) {
    rig.audio.setMultiplier(value);
    rig.advance(frameMs);
  }
  rig.audio.setMultiplier(target);
  rig.advance(frameMs);
}

export const names = (log: readonly CueEntry[]): CueName[] => log.map((entry) => entry.name);

/** Entries of the cue log logged after the first `count` ones. */
export const loggedSince = (audio: PipaAudio, count: number): CueEntry[] => audio.cueLog().slice(count);

/** True when `wanted` appears in `actual` in this order (other entries may sit in between). */
export function isSubsequence(actual: readonly string[], wanted: readonly string[]): boolean {
  let next = 0;
  for (const name of actual) if (name === wanted[next]) next += 1;
  return next === wanted.length;
}

/** In-memory Web Storage (the part the preference uses), recording every write. */
export class MemoryStorage implements PreferenceStorage {
  readonly values = new Map<string, string>();
  readonly writes: { readonly key: string; readonly value: string }[] = [];

  getItem(key: string): string | null {
    return this.values.get(key) ?? null;
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value);
    this.writes.push({ key, value });
  }
}

/** A storage that throws on every access (private mode, blocked site data). */
export class ThrowingStorage implements PreferenceStorage {
  getItem(key: string): string | null {
    throw new Error(`SecurityError: storage blocked (${key})`);
  }

  setItem(key: string, value: string): void {
    throw new Error(`QuotaExceededError: storage blocked (${key}=${value})`);
  }
}

/** A document whose visibility the test switches. */
export class FakeDocument extends EventTarget implements VisibilitySource {
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

/** Highest value of `read` sampled every millisecond in [fromMs, toMs] (audio clock). */
export function peakOver(read: (seconds: number) => number, fromMs: number, toMs: number): number {
  let peak = -Infinity;
  for (let ms = fromMs; ms <= toMs; ms += 1) peak = Math.max(peak, read(ms / 1000));
  return peak;
}

/** A group of sources started at the same audio time: one cue as heard (a voice may stack several sources). */
export interface StartedCue {
  readonly startMs: number;
  readonly sources: readonly FakeSourceNode[];
  /** Highest summed level of the group at the destination, sampled every millisecond until `untilMs`. */
  readonly peak: number;
}

/** The sources started in the context grouped by start time (1 ms), in start order, with their peak level. */
export function startedCues(context: FakeAudioContext, untilMs: number): StartedCue[] {
  const groups = new Map<number, FakeSourceNode[]>();
  for (const source of context.started) {
    const startMs = Math.round((source.startTime ?? 0) * 1000);
    groups.set(startMs, [...(groups.get(startMs) ?? []), source]);
  }
  return [...groups.entries()]
    .sort(([a], [b]) => a - b)
    .map(([startMs, sources]) => ({
      startMs,
      sources,
      peak: peakOver((t) => sources.reduce((sum, source) => sum + context.level(source, t), 0), startMs, untilMs),
    }));
}
