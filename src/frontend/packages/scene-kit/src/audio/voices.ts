// Voices (SPEC-0018 §4): the synthesized sounds of the Pipa — filtered noise, oscillators and envelopes, no files
// (ADR-0024). Each function schedules its nodes on the audio clock and returns what it scheduled, so PipaAudio logs the
// cue with the same times. Every source has a peak amplitude ≤ 1, so the gains below are also the loudest each voice
// can reach the master with (the safe-peak budget of SPEC-0018 UT-05).

import type {
  AudioBufferLike,
  AudioContextLike,
  AudioNodeLike,
  AudioParamLike,
  AudioScheduledSourceNodeLike,
  GainNodeLike,
} from "./web-audio";
import { tensionHz, windGain } from "./audio-mix";

/** Peak gain of each voice before the master (levels balanced so consecutive cues stay within 6 dB). */
export const voiceLevels = {
  /** Tension of the line (constant; its pitch carries the risk). */
  tension: 0.1,
  /** Takeoff pull. */
  whoosh: 0.25,
  /** Milestone "plim" (fundamental + octave). */
  milestone: 0.25,
  /** Line snap of the crash. */
  snap: 0.35,
  /** Descending fall of the crash. */
  fall: 0.25,
  /** Calm restart chime. */
  restart: 0.18,
} as const;

/** Lengths of the one-shot cues, seconds (SPEC-0018 §2 and §6). */
export const cueSeconds = {
  /** Takeoff pull: ≤ 600 ms. */
  whoosh: 0.55,
  milestone: 0.5,
  /** Snap: ≤ 150 ms. */
  snap: 0.12,
  /** The fall starts this long after the snap… */
  fallDelay: 0.02,
  /** …and ends just before the silence, 1500 ms after the crash. */
  fall: 1.47,
  /** Silence from this long after the crash. */
  silence: 1.5,
  /** Restart chime: ≤ 600 ms. */
  restart: 0.5,
  /** Wind and tension reach 0 this long after the crash (≤ 100 ms). */
  crashFade: 0.08,
} as const;

/** Time constant of the continuous changes driven by the multiplier, seconds (smooth, no zipper noise). */
const glide = 0.06;
/** Time constant of the fade-in of a loop, seconds (quick, but without a click). */
const fadeIn = 0.015;
/** Smallest value an exponential ramp may reach (it can never reach 0). */
const floor = 0.0001;

/** Noise buffers shared by every voice of a context. */
export interface NoiseBank {
  /** White noise, peak 1 (snap). */
  readonly white: AudioBufferLike;
  /** Brown (integrated) noise, peak 1: the soft rumble of the wind and of the takeoff pull. */
  readonly brown: AudioBufferLike;
}

/** Deterministic noise (mulberry32), so a rendered round sounds the same every time. */
function random(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4_294_967_296;
  };
}

function normalize(data: Float32Array): void {
  let peak = 0;
  for (const sample of data) peak = Math.max(peak, Math.abs(sample));
  if (peak === 0) return;
  for (let index = 0; index < data.length; index += 1) data[index] = (data[index] ?? 0) / peak;
}

export function createNoiseBank(context: AudioContextLike): NoiseBank {
  const rate = context.sampleRate;
  const white = context.createBuffer(1, Math.round(rate), rate);
  const whiteData = white.getChannelData(0);
  const next = random(0x5eed);
  for (let index = 0; index < whiteData.length; index += 1) whiteData[index] = next() * 2 - 1;
  normalize(whiteData);

  const brown = context.createBuffer(1, Math.round(rate * 2), rate);
  const brownData = brown.getChannelData(0);
  let last = 0;
  for (let index = 0; index < brownData.length; index += 1) {
    last = (last + 0.02 * (next() * 2 - 1)) / 1.02;
    brownData[index] = last;
  }
  normalize(brownData);
  return { white, brown };
}

/** A smooth glide from the value at `at` towards `target`. */
function glideTo(param: AudioParamLike, target: number, at: number, timeConstant = glide): void {
  param.setTargetAtTime(target, at, timeConstant);
}

/** A loop of the round: stopped together when the round's sounds end. */
export interface Loop {
  stop(at: number): void;
}

/** Wind: brown noise looping through a lowpass that opens with the multiplier, with slow gusts. */
export interface WindVoice extends Loop {
  setMultiplier(multiplier: number, at: number): void;
}

const windCutoffHz = (multiplier: number): number =>
  500 + 1400 * ((windGain(multiplier) - 0.15) / 0.45);

export function startWind(
  context: AudioContextLike,
  output: AudioNodeLike,
  noise: NoiseBank,
  multiplier: number,
  at: number,
): WindVoice {
  const source = context.createBufferSource();
  source.buffer = noise.brown;
  source.loop = true;
  const filter = context.createBiquadFilter();
  filter.type = "lowpass";
  filter.Q.setValueAtTime(0.7, at);
  filter.frequency.setValueAtTime(windCutoffHz(multiplier), at);
  const gain = context.createGain();
  gain.gain.setValueAtTime(0, at);
  glideTo(gain.gain, windGain(multiplier), at, fadeIn);
  // Gusts: a slow oscillator moving the cutoff (modulates a param, so it adds nothing to the level).
  const gust = context.createOscillator();
  gust.type = "sine";
  gust.frequency.setValueAtTime(0.23, at);
  const gustDepth = context.createGain();
  gustDepth.gain.setValueAtTime(160, at);
  gust.connect(gustDepth);
  gustDepth.connect(filter.frequency);

  source.connect(filter);
  filter.connect(gain);
  gain.connect(output);
  source.start(at);
  gust.start(at);
  return {
    setMultiplier(value, when) {
      glideTo(gain.gain, windGain(value), when);
      glideTo(filter.frequency, windCutoffHz(value), when);
    },
    stop(when) {
      source.stop(when);
      gust.stop(when);
    },
  };
}

/** Tension of the line: a soft triangle whose pitch rises with log(m). */
export interface TensionVoice extends Loop {
  setMultiplier(multiplier: number, at: number): void;
}

export function startTension(
  context: AudioContextLike,
  output: AudioNodeLike,
  multiplier: number,
  at: number,
): TensionVoice {
  const oscillator = context.createOscillator();
  oscillator.type = "triangle";
  oscillator.frequency.setValueAtTime(tensionHz(multiplier), at);
  const gain = context.createGain();
  gain.gain.setValueAtTime(0, at);
  glideTo(gain.gain, voiceLevels.tension, at, fadeIn);
  oscillator.connect(gain);
  gain.connect(output);
  oscillator.start(at);
  return {
    setMultiplier(value, when) {
      glideTo(oscillator.frequency, tensionHz(value), when);
    },
    stop(when) {
      oscillator.stop(when);
    },
  };
}

/** An envelope gain node: 0 at `at`, up to `peak` after `attack`, back to 0 at `at + length`. */
function envelope(
  context: AudioContextLike,
  output: AudioNodeLike,
  at: number,
  peak: number,
  attack: number,
  length: number,
): GainNodeLike {
  const gain = context.createGain();
  gain.gain.setValueAtTime(0, at);
  gain.gain.linearRampToValueAtTime(peak, at + attack);
  gain.gain.exponentialRampToValueAtTime(floor, at + length * 0.95);
  gain.gain.linearRampToValueAtTime(0, at + length);
  gain.connect(output);
  return gain;
}

function startAndStop(source: AudioScheduledSourceNodeLike, at: number, length: number): void {
  source.start(at);
  source.stop(at + length);
}

/** Takeoff pull: a band of brown noise sweeping up and back. Returns its length, seconds. */
export function playWhoosh(
  context: AudioContextLike,
  output: AudioNodeLike,
  noise: NoiseBank,
  at: number,
): number {
  const length = cueSeconds.whoosh;
  const source = context.createBufferSource();
  source.buffer = noise.brown;
  const filter = context.createBiquadFilter();
  filter.type = "bandpass";
  filter.Q.setValueAtTime(1.2, at);
  filter.frequency.setValueAtTime(250, at);
  filter.frequency.exponentialRampToValueAtTime(1400, at + length * 0.6);
  filter.frequency.exponentialRampToValueAtTime(500, at + length);
  const gain = context.createGain();
  gain.gain.setValueAtTime(0, at);
  gain.gain.linearRampToValueAtTime(voiceLevels.whoosh, at + length * 0.35);
  gain.gain.linearRampToValueAtTime(0, at + length);
  source.connect(filter);
  filter.connect(gain);
  gain.connect(output);
  startAndStop(source, at, length);
  return length;
}

/** Milestone "plim": a sine and its octave with a bell-like decay. Returns its length, seconds. */
export function playMilestone(
  context: AudioContextLike,
  output: AudioNodeLike,
  hz: number,
  at: number,
): number {
  const length = cueSeconds.milestone;
  const bell = envelope(context, output, at, voiceLevels.milestone, 0.006, length);
  for (const [ratio, share] of [
    [1, 0.65],
    [2, 0.35],
  ] as const) {
    const oscillator = context.createOscillator();
    oscillator.type = "sine";
    oscillator.frequency.setValueAtTime(hz * ratio, at);
    const mix = context.createGain();
    mix.gain.setValueAtTime(share, at);
    oscillator.connect(mix);
    mix.connect(bell);
    startAndStop(oscillator, at, length);
  }
  return length;
}

/** Snap of the line: a dry burst of high white noise. Returns its length, seconds. */
export function playSnap(
  context: AudioContextLike,
  output: AudioNodeLike,
  noise: NoiseBank,
  at: number,
): number {
  const length = cueSeconds.snap;
  const source = context.createBufferSource();
  source.buffer = noise.white;
  const filter = context.createBiquadFilter();
  filter.type = "highpass";
  filter.frequency.setValueAtTime(1800, at);
  filter.Q.setValueAtTime(0.9, at);
  source.connect(filter);
  filter.connect(envelope(context, output, at, voiceLevels.snap, 0.002, length));
  // Stopped a little after its envelope closes (the snap is silent from `length`).
  startAndStop(source, at, length + 0.02);
  return length;
}

/** Fall of the kite: a soft triangle gliding down. Returns its length, seconds. */
export function playFall(context: AudioContextLike, output: AudioNodeLike, at: number): number {
  const length = cueSeconds.fall;
  const oscillator = context.createOscillator();
  oscillator.type = "triangle";
  oscillator.frequency.setValueAtTime(660, at);
  oscillator.frequency.exponentialRampToValueAtTime(90, at + length);
  const filter = context.createBiquadFilter();
  filter.type = "lowpass";
  filter.frequency.setValueAtTime(1800, at);
  const gain = context.createGain();
  gain.gain.setValueAtTime(0, at);
  gain.gain.linearRampToValueAtTime(voiceLevels.fall, at + 0.06);
  gain.gain.linearRampToValueAtTime(0, at + length);
  oscillator.connect(filter);
  filter.connect(gain);
  gain.connect(output);
  startAndStop(oscillator, at, length);
  return length;
}

/** Calm restart chime: a soft fifth (G5 + D6). Returns its length, seconds. */
export function playRestart(context: AudioContextLike, output: AudioNodeLike, at: number): number {
  const length = cueSeconds.restart;
  const chime = context.createGain();
  chime.gain.setValueAtTime(0, at);
  chime.gain.linearRampToValueAtTime(voiceLevels.restart, at + 0.04);
  chime.gain.exponentialRampToValueAtTime(floor, at + length * 0.95);
  chime.gain.linearRampToValueAtTime(0, at + length);
  chime.connect(output);
  for (const [hz, share] of [
    [783.99, 0.6],
    [1174.66, 0.4],
  ] as const) {
    const oscillator = context.createOscillator();
    oscillator.type = "sine";
    oscillator.frequency.setValueAtTime(hz, at);
    const mix = context.createGain();
    mix.gain.setValueAtTime(share, at);
    oscillator.connect(mix);
    mix.connect(chime);
    startAndStop(oscillator, at, length);
  }
  return length;
}
