// The part of the Web Audio API the sound engine uses (SPEC-0018 §4, ADR-0024). A real AudioContext (and an
// OfflineAudioContext, for rendering) satisfies these types; the unit tests drive the engine with a hand-written double
// of this same surface that records every automation (SPEC-0018 §7.6, amendment IMP-01).

export interface AudioParamLike {
  value: number;
  readonly defaultValue: number;
  setValueAtTime(value: number, startTime: number): unknown;
  linearRampToValueAtTime(value: number, endTime: number): unknown;
  exponentialRampToValueAtTime(value: number, endTime: number): unknown;
  setTargetAtTime(target: number, startTime: number, timeConstant: number): unknown;
  setValueCurveAtTime(values: number[], startTime: number, duration: number): unknown;
  cancelScheduledValues(cancelTime: number): unknown;
  cancelAndHoldAtTime(cancelTime: number): unknown;
}

export interface AudioNodeLike {
  connect(destination: AudioNodeLike): AudioNodeLike;
  connect(destination: AudioParamLike): void;
  disconnect(): void;
}

export interface GainNodeLike extends AudioNodeLike {
  readonly gain: AudioParamLike;
}

export interface BiquadFilterNodeLike extends AudioNodeLike {
  type: BiquadFilterType;
  readonly frequency: AudioParamLike;
  readonly Q: AudioParamLike;
  readonly gain: AudioParamLike;
  readonly detune: AudioParamLike;
}

export interface AudioScheduledSourceNodeLike extends AudioNodeLike {
  start(when?: number): void;
  stop(when?: number): void;
}

export interface OscillatorNodeLike extends AudioScheduledSourceNodeLike {
  type: OscillatorType;
  readonly frequency: AudioParamLike;
  readonly detune: AudioParamLike;
}

export interface AudioBufferLike {
  readonly length: number;
  readonly sampleRate: number;
  readonly numberOfChannels: number;
  readonly duration: number;
  getChannelData(channel: number): Float32Array;
}

export interface AudioBufferSourceNodeLike extends AudioScheduledSourceNodeLike {
  buffer: AudioBufferLike | null;
  loop: boolean;
  readonly playbackRate: AudioParamLike;
  readonly detune: AudioParamLike;
}

export interface AudioContextLike {
  /** Audio clock, seconds. */
  readonly currentTime: number;
  readonly sampleRate: number;
  readonly state: AudioContextState;
  readonly destination: AudioNodeLike;
  createGain(): GainNodeLike;
  createBiquadFilter(): BiquadFilterNodeLike;
  createOscillator(): OscillatorNodeLike;
  createBuffer(numberOfChannels: number, length: number, sampleRate: number): AudioBufferLike;
  createBufferSource(): AudioBufferSourceNodeLike;
  resume(): Promise<void>;
  suspend(): Promise<void>;
  close(): Promise<void>;
}
