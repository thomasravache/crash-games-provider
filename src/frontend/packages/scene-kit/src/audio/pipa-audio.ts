// PipaAudio (SPEC-0018 §4): the GameAudio of the Pipa, composed of AudioMix, CueScheduler and the synthesized Voices,
// behind an AudioGate that creates/resumes the AudioContext only in unlock().
//
// Graph contract (what the tests observe): every voice reaches the destination through one master GainNode, the only
// node connected directly to `destination`; its gain is volume · (muted ? 0 : 1) · 0.8, changed with 50 ms ramps.
//
// Sound is scheduled only while "audible" (unlocked, with a context, not muted, tab visible). State calls always update
// the round (phase, multiplier, milestones already crossed), but only an audible engine starts sources and logs cues;
// when it becomes audible again (unlock, unmute, tab visible) it plays only the loops of the current state, never the
// one-shot cues it missed.

import type { VisibilitySource } from "../kite/visibility";
import { masterGain, masterRampMs } from "./audio-mix";
import { createCueScheduler, type MilestoneCue } from "./cue-scheduler";
import type { AudioState, AudioTestHook, CueEntry, CueName, GameAudio } from "./game-audio";
import {
  loadSoundPreference,
  normalizeVolume,
  saveSoundPreference,
  type PreferenceStorage,
  type SoundPreference,
} from "./sound-preference";
import {
  createNoiseBank,
  cueSeconds,
  playFall,
  playMilestone,
  playRestart,
  playSnap,
  playWhoosh,
  startTension,
  startWind,
  type NoiseBank,
  type TensionVoice,
  type WindVoice,
} from "./voices";
import type { AudioContextLike, GainNodeLike } from "./web-audio";

export interface PipaAudioOptions {
  /**
   * Creates the AudioContext; called at most once, and only by unlock(). Absent: `new AudioContext()` when the browser
   * has one. Absent without Web Audio, throwing or returning null: the game stays muted, without errors.
   */
  readonly createContext?: () => AudioContextLike | null;
  /** Where the preference "apogeu.sound" is kept. Absent: window.localStorage when reachable. null: not kept. */
  readonly storage?: PreferenceStorage | null;
  /** Hidden tab: the context is suspended while hidden. Absent: the document when there is one. null: not watched. */
  readonly visibility?: VisibilitySource | null;
}

export interface PipaAudio extends GameAudio, AudioTestHook {
  /** The current sound preference (loaded from the storage at creation; default {muted:false, volume:0.7}). */
  preference(): SoundPreference;
}

type RoundPhase = "idle" | "betting" | "flying" | "crashed";

/** Milestones crossed in the same tick (a jump after a resync) sound as a quick arpeggio, this far apart, seconds. */
const arpeggioSeconds = 0.09;
/** Fade of the loops when a flight ends without a crash (reset, resync), seconds. */
const releaseSeconds = 0.08;

/** The browser's AudioContext, when there is one (a real AudioContext satisfies AudioContextLike). */
function browserContext(): AudioContextLike | null {
  const constructor = (globalThis as { AudioContext?: new () => AudioContextLike }).AudioContext;
  return constructor === undefined ? null : new constructor();
}

function browserStorage(): PreferenceStorage | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    // Blocked site data: reading window.localStorage itself throws.
    return null;
  }
}

function browserVisibility(): VisibilitySource | null {
  return typeof document === "undefined" ? null : document;
}

/** The loops of the round being heard, behind one bus faded out together at the crash. */
interface Bed {
  readonly bus: GainNodeLike;
  readonly wind: WindVoice;
  tension: TensionVoice | null;
}

/** Ramps `gain` linearly from its value now to `target` in `seconds` (cancelling what was scheduled after now). */
function rampGain(gain: GainNodeLike, now: number, target: number, seconds: number): void {
  const current = gain.gain.value;
  gain.gain.cancelScheduledValues(now);
  gain.gain.setValueAtTime(current, now);
  gain.gain.linearRampToValueAtTime(target, now + seconds);
}

export function createPipaAudio(options: PipaAudioOptions = {}): PipaAudio {
  const createContext = options.createContext ?? browserContext;
  const storage = options.storage === undefined ? browserStorage() : options.storage;
  const visibility = options.visibility === undefined ? browserVisibility() : options.visibility;

  let preference = loadSoundPreference(storage);
  let phase: RoundPhase = "idle";
  let multiplier = 1;
  const scheduler = createCueScheduler();
  const log: CueEntry[] = [];

  let unlocking: Promise<void> | null = null;
  let disposed = false;
  let hidden = visibility?.visibilityState === "hidden";
  let context: AudioContextLike | null = null;
  let master: GainNodeLike | null = null;
  let noise: NoiseBank | null = null;
  let bed: Bed | null = null;

  /** The context, when sound may be scheduled now. */
  const audible = (): AudioContextLike | null =>
    disposed || hidden || preference.muted ? null : context;

  /** Logs a cue at `at` seconds (+ `offsetMs`) of the audio clock. */
  const record = (
    name: CueName,
    at: number,
    extra: Omit<CueEntry, "name" | "atMs"> = {},
    offsetMs = 0,
  ): void => {
    log.push({ name, atMs: at * 1000 + offsetMs, ...extra });
  };

  /** Master gain to the preference with a 50 ms ramp, from its value now (no clicks). */
  const rampMaster = (): void => {
    if (context === null || master === null) return;
    rampGain(
      master,
      context.currentTime,
      masterGain(preference.volume, preference.muted),
      masterRampMs / 1000,
    );
  };

  /** Stops the loops of the bed at `stopAt`, fading its bus out in `fadeSeconds` from now when given. */
  const stopBed = (stopAt: number, fadeSeconds = 0): void => {
    const current = bed;
    if (current === null || context === null) return;
    bed = null;
    if (fadeSeconds > 0) rampGain(current.bus, context.currentTime, 0, fadeSeconds);
    current.wind.stop(stopAt);
    current.tension?.stop(stopAt);
  };

  /** The wind of the round, started when it is not playing yet. */
  const ensureBed = (ctx: AudioContextLike, at: number): Bed | null => {
    if (bed !== null) return bed;
    if (master === null || noise === null) return null;
    const bus = ctx.createGain();
    bus.gain.setValueAtTime(1, at);
    bus.connect(master);
    bed = { bus, wind: startWind(ctx, bus, noise, multiplier, at), tension: null };
    record("wind", at);
    return bed;
  };

  const ensureTension = (ctx: AudioContextLike, current: Bed, at: number): void => {
    if (current.tension !== null) return;
    current.tension = startTension(ctx, current.bus, multiplier, at);
    record("tension", at);
  };

  /** Plays only the loops of the current state (unlock, unmute, tab visible again). */
  const playCurrentState = (): void => {
    const ctx = audible();
    if (ctx === null || (phase !== "betting" && phase !== "flying")) return;
    const now = ctx.currentTime;
    const current = ensureBed(ctx, now);
    if (current !== null && phase === "flying") ensureTension(ctx, current, now);
  };

  const playMilestones = (
    ctx: AudioContextLike,
    output: GainNodeLike,
    cues: readonly MilestoneCue[],
  ): void => {
    cues.forEach((cue, order) => {
      const at = ctx.currentTime + order * arpeggioSeconds;
      const length = playMilestone(ctx, output, cue.hz, at);
      record("milestone", at, { durationMs: length * 1000, milestone: cue.milestone, hz: cue.hz });
    });
  };

  /** snap → fall → silence, on the audio clock; the loops of the flight fade to 0 meanwhile. */
  const playCrash = (ctx: AudioContextLike): void => {
    if (master === null || noise === null) return;
    const now = ctx.currentTime;
    stopBed(now + cueSeconds.crashFade + 0.02, cueSeconds.crashFade);
    const snap = playSnap(ctx, master, noise, now);
    record("snap", now, { durationMs: snap * 1000 });
    const fallAt = now + cueSeconds.fallDelay;
    const fall = playFall(ctx, master, fallAt);
    record("fall", fallAt, { durationMs: fall * 1000 });
    record("silence", now, {}, cueSeconds.silence * 1000);
  };

  const onVisibilityChange = (): void => {
    const nowHidden = visibility?.visibilityState === "hidden";
    if (nowHidden === hidden) return;
    hidden = nowHidden;
    if (context === null || disposed) return;
    if (hidden) {
      // Nothing of the hidden time is heard later: the loops stop and the audio clock stops with the context.
      stopBed(context.currentTime);
      context.suspend().catch(() => undefined);
    } else {
      context.resume().catch(() => undefined);
      playCurrentState();
    }
  };
  visibility?.addEventListener("visibilitychange", onVisibilityChange);

  /** The AudioGate: creates the context once, in the player's gesture, and starts the current state. */
  const openGate = (): Promise<void> => {
    let created: AudioContextLike | null;
    try {
      created = createContext();
    } catch {
      // Web Audio blocked or unavailable: the game goes on silent (SPEC-0018 §5).
      created = null;
    }
    if (created === null) return Promise.resolve();
    context = created;
    master = created.createGain();
    master.gain.setValueAtTime(0, created.currentTime);
    master.connect(created.destination);
    rampMaster();
    noise = createNoiseBank(created);
    // Synchronously, so the state calls made in the same gesture already sound.
    playCurrentState();
    const started = hidden ? created.suspend() : created.resume();
    return started.catch(() => undefined);
  };

  return {
    unlock() {
      if (disposed) return Promise.resolve();
      if (unlocking === null) {
        unlocking = openGate();
        return unlocking;
      }
      // A later gesture retries a context the browser kept suspended (a gesture that did not count as activation).
      if (context !== null && !hidden && context.state === "suspended") {
        return context.resume().catch(() => undefined);
      }
      return unlocking;
    },

    prepare() {
      if (disposed || phase === "betting") return;
      const afterCrash = phase === "crashed";
      phase = "betting";
      multiplier = 1;
      scheduler.reset();
      const ctx = audible();
      if (ctx === null) return;
      const now = ctx.currentTime;
      if (bed !== null) {
        // A flight that ended without a crash (resync): back to the betting wind.
        bed.tension?.stop(now + releaseSeconds);
        bed.tension = null;
        bed.wind.setMultiplier(1, now);
      }
      if (afterCrash && master !== null) {
        const length = playRestart(ctx, master, now);
        record("restart", now, { durationMs: length * 1000 });
      }
      ensureBed(ctx, now);
    },

    // The pull follows the scene's takeoff now: the start time only matters to the flight curve of the store.
    takeoff() {
      if (disposed || phase === "flying") return;
      phase = "flying";
      multiplier = 1;
      scheduler.reset();
      const ctx = audible();
      if (ctx === null || noise === null) return;
      const now = ctx.currentTime;
      const current = ensureBed(ctx, now);
      if (current === null) return;
      const length = playWhoosh(ctx, current.bus, noise, now);
      record("takeoff", now, { durationMs: length * 1000 });
      ensureTension(ctx, current, now);
    },

    setMultiplier(value) {
      if (disposed || phase !== "flying" || !Number.isFinite(value)) return;
      multiplier = value;
      // Milestones are marked even when nothing is heard, so a missed one is never played later.
      const cues = scheduler.update(value);
      const ctx = audible();
      if (ctx === null || bed === null) return;
      const now = ctx.currentTime;
      bed.wind.setMultiplier(value, now);
      bed.tension?.setMultiplier(value, now);
      if (cues.length > 0) playMilestones(ctx, bed.bus, cues);
    },

    // The script is the same at any crash point (the point is shown by the HUD).
    crash() {
      if (disposed || phase === "crashed") return;
      phase = "crashed";
      const ctx = audible();
      if (ctx !== null) playCrash(ctx);
    },

    reset() {
      if (disposed) return;
      phase = "idle";
      multiplier = 1;
      scheduler.reset();
      if (context !== null) stopBed(context.currentTime + releaseSeconds + 0.02, releaseSeconds);
    },

    setMuted(muted) {
      if (disposed || muted === preference.muted) return;
      preference = { ...preference, muted };
      saveSoundPreference(storage, preference);
      rampMaster();
      if (context === null) return;
      if (muted) stopBed(context.currentTime + masterRampMs / 1000);
      else playCurrentState();
    },

    setVolume(volume) {
      const normalized = normalizeVolume(volume);
      if (disposed || normalized === null) return;
      preference = { ...preference, volume: normalized };
      saveSoundPreference(storage, preference);
      rampMaster();
    },

    dispose() {
      if (disposed) return;
      disposed = true;
      visibility?.removeEventListener("visibilitychange", onVisibilityChange);
      if (context === null) return;
      stopBed(context.currentTime);
      context.close().catch(() => undefined);
    },

    preference: () => preference,

    cueLog: () => log,

    state(): AudioState {
      if (preference.muted) return "muted";
      if (unlocking === null || context === null) return "locked";
      return hidden || disposed ? "suspended" : "running";
    },
  };
}
