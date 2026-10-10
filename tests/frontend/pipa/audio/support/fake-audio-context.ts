// Hand-written double of the OfflineAudioContext/AudioParam surface the sound engine uses (SPEC-0018 §7.6, amendment
// IMP-01): Node has no Web Audio, so instead of rendering samples the double records the graph (connections, sources
// started and stopped) and every AudioParam automation, and evaluates them deterministically:
// - `FakeAudioParam.at(t)` is the value of the param at audio time `t` (seconds), following the Web Audio automation
//   rules for setValueAtTime, linear/exponential ramps, setTargetAtTime, value curves and cancellations;
// - `FakeAudioContext.level(source, t)` is how loud a started source reaches the destination at `t`: the sum, over every
//   path from the source to the destination, of the product of the GainNode gains on it (sources count as amplitude 1;
//   filters as 1). Connections into an AudioParam (modulation) do not reach the destination.
// The audio clock only moves through `advance(ms)`, and only while the context is "running".
import type {
  AudioBufferLike,
  AudioBufferSourceNodeLike,
  AudioContextLike,
  AudioNodeLike,
  AudioParamLike,
  BiquadFilterNodeLike,
  GainNodeLike,
  OscillatorNodeLike,
} from "./audio-module";

export type Automation =
  | { readonly kind: "set"; readonly time: number; readonly value: number }
  | { readonly kind: "linear" | "exponential"; readonly time: number; readonly value: number; readonly from: number }
  | { readonly kind: "target"; readonly time: number; readonly value: number; readonly timeConstant: number }
  | { readonly kind: "curve"; readonly time: number; readonly values: readonly number[]; readonly duration: number };

export class FakeAudioParam implements AudioParamLike {
  private events: Automation[] = [];

  constructor(
    private readonly context: FakeAudioContext,
    readonly defaultValue: number,
  ) {}

  get value(): number {
    return this.at(this.context.currentTime);
  }

  set value(value: number) {
    this.setValueAtTime(value, this.context.currentTime);
  }

  /** Every automation scheduled so far, in time order. */
  automation(): readonly Automation[] {
    return this.events;
  }

  setValueAtTime(value: number, startTime: number): this {
    return this.insert({ kind: "set", time: startTime, value });
  }

  linearRampToValueAtTime(value: number, endTime: number): this {
    return this.insert({ kind: "linear", time: endTime, value, from: this.context.currentTime });
  }

  exponentialRampToValueAtTime(value: number, endTime: number): this {
    // As the browser: an exponential ramp cannot reach (or start from) zero.
    if (value === 0) throw new RangeError("exponentialRampToValueAtTime: the value must not be 0");
    return this.insert({ kind: "exponential", time: endTime, value, from: this.context.currentTime });
  }

  setTargetAtTime(target: number, startTime: number, timeConstant: number): this {
    return this.insert({ kind: "target", time: startTime, value: target, timeConstant });
  }

  setValueCurveAtTime(values: number[], startTime: number, duration: number): this {
    return this.insert({ kind: "curve", time: startTime, values: [...values], duration });
  }

  cancelScheduledValues(cancelTime: number): this {
    this.events = this.events.filter((event) => event.time < cancelTime);
    return this;
  }

  cancelAndHoldAtTime(cancelTime: number): this {
    const held = this.at(cancelTime);
    this.events = this.events.filter((event) => event.time < cancelTime);
    return this.insert({ kind: "set", time: cancelTime, value: held });
  }

  /** Value at audio time `t`, seconds. */
  at(t: number): number {
    // State at the end of the last event processed: a held value, or a target/curve process started at `start`.
    let held = this.defaultValue;
    let start = 0;
    let process: Automation | undefined;
    const evaluate = (time: number): number => {
      if (process?.kind === "target") {
        return process.value + (held - process.value) * Math.exp(-(time - start) / process.timeConstant);
      }
      if (process?.kind === "curve") {
        const { values, duration } = process;
        if (values.length === 0) return held;
        const progress = Math.min(1, Math.max(0, (time - start) / duration));
        const position = progress * (values.length - 1);
        const index = Math.floor(position);
        const low = values[index] ?? held;
        const high = values[Math.min(values.length - 1, index + 1)] ?? low;
        return low + (high - low) * (position - index);
      }
      return held;
    };

    let previousTime: number | undefined;
    for (const event of this.events) {
      if (event.kind === "linear" || event.kind === "exponential") {
        const rampStart = previousTime ?? event.from;
        const rampFrom = evaluate(rampStart);
        if (t < event.time) {
          if (t <= rampStart) return evaluate(t);
          const progress = (t - rampStart) / (event.time - rampStart);
          if (event.kind === "linear") return rampFrom + (event.value - rampFrom) * progress;
          if (rampFrom === 0 || event.value === 0 || rampFrom * event.value < 0) return rampFrom;
          return rampFrom * (event.value / rampFrom) ** progress;
        }
        held = event.value;
        start = event.time;
        process = undefined;
        previousTime = event.time;
        continue;
      }
      if (t < event.time) return evaluate(t);
      const valueAtEvent = evaluate(event.time);
      if (event.kind === "set") {
        held = event.value;
        process = undefined;
      } else if (event.kind === "target") {
        held = valueAtEvent;
        process = event;
      } else if (event.kind === "curve") {
        held = event.values.at(-1) ?? valueAtEvent;
        process = event;
        if (t >= event.time + event.duration) {
          process = undefined;
        }
      }
      start = event.time;
      previousTime = event.kind === "curve" ? event.time + event.duration : event.time;
    }
    if (process?.kind === "curve" && t >= start + process.duration) return process.values.at(-1) ?? held;
    return evaluate(t);
  }

  private insert(event: Automation): this {
    // Stable: events at the same time keep the order they were scheduled in.
    const index = this.events.findIndex((other) => other.time > event.time);
    if (index < 0) this.events.push(event);
    else this.events.splice(index, 0, event);
    return this;
  }
}

export class FakeAudioNode implements AudioNodeLike {
  readonly outputs = new Set<FakeAudioNode>();
  readonly modulates = new Set<FakeAudioParam>();

  constructor(
    readonly context: FakeAudioContext,
    readonly kind: string,
  ) {}

  connect(destination: AudioNodeLike): AudioNodeLike;
  connect(destination: AudioParamLike): void;
  connect(destination: AudioNodeLike | AudioParamLike): AudioNodeLike | undefined {
    if (destination instanceof FakeAudioNode) {
      this.outputs.add(destination);
      return destination;
    }
    if (destination instanceof FakeAudioParam) {
      this.modulates.add(destination);
      return undefined;
    }
    throw new Error("FakeAudioNode.connect: destination of another context");
  }

  disconnect(): void {
    this.outputs.clear();
    this.modulates.clear();
  }

  /** Multiplier this node applies to what goes through it at `t` (1 for every node but GainNode). */
  gainAt(t: number): number {
    void t;
    return 1;
  }
}

export class FakeGainNode extends FakeAudioNode implements GainNodeLike {
  readonly gain: FakeAudioParam;

  constructor(context: FakeAudioContext) {
    super(context, "gain");
    this.gain = new FakeAudioParam(context, 1);
  }

  override gainAt(t: number): number {
    return this.gain.at(t);
  }
}

export class FakeBiquadFilterNode extends FakeAudioNode implements BiquadFilterNodeLike {
  type: BiquadFilterType = "lowpass";
  readonly frequency: FakeAudioParam;
  readonly Q: FakeAudioParam;
  readonly gain: FakeAudioParam;
  readonly detune: FakeAudioParam;

  constructor(context: FakeAudioContext) {
    super(context, "biquad");
    this.frequency = new FakeAudioParam(context, 350);
    this.Q = new FakeAudioParam(context, 1);
    this.gain = new FakeAudioParam(context, 0);
    this.detune = new FakeAudioParam(context, 0);
  }
}

/** A source node: plays from `startTime` until `stopTime` (audio seconds). */
export class FakeSourceNode extends FakeAudioNode {
  startTime: number | undefined;
  stopTime = Infinity;
  /** Audio time at which start() was called. */
  startCalledAt: number | undefined;

  start(when = 0): void {
    if (this.startTime !== undefined) throw new Error("InvalidStateError: start() called twice");
    this.startCalledAt = this.context.currentTime;
    this.startTime = Math.max(when, this.context.currentTime);
    this.context.started.push(this);
  }

  stop(when = 0): void {
    if (this.startTime === undefined) throw new Error("InvalidStateError: stop() before start()");
    this.stopTime = Math.max(when, this.context.currentTime);
  }

  playingAt(t: number): boolean {
    return this.startTime !== undefined && t >= this.startTime && t < this.stopTime;
  }

  /** The param that sets the pitch of the source (frequency or playback rate). */
  pitch(): FakeAudioParam | undefined {
    return undefined;
  }
}

export class FakeOscillatorNode extends FakeSourceNode implements OscillatorNodeLike {
  type: OscillatorType = "sine";
  readonly frequency: FakeAudioParam;
  readonly detune: FakeAudioParam;

  constructor(context: FakeAudioContext) {
    super(context, "oscillator");
    this.frequency = new FakeAudioParam(context, 440);
    this.detune = new FakeAudioParam(context, 0);
  }

  override pitch(): FakeAudioParam {
    return this.frequency;
  }
}

export class FakeAudioBuffer implements AudioBufferLike {
  private readonly channels: Float32Array[];

  constructor(
    readonly numberOfChannels: number,
    readonly length: number,
    readonly sampleRate: number,
  ) {
    this.channels = Array.from({ length: numberOfChannels }, () => new Float32Array(length));
  }

  get duration(): number {
    return this.length / this.sampleRate;
  }

  getChannelData(channel: number): Float32Array {
    const data = this.channels[channel];
    if (data === undefined) throw new Error(`IndexSizeError: channel ${String(channel)}`);
    return data;
  }
}

export class FakeAudioBufferSourceNode extends FakeSourceNode implements AudioBufferSourceNodeLike {
  buffer: AudioBufferLike | null = null;
  loop = false;
  readonly playbackRate: FakeAudioParam;
  readonly detune: FakeAudioParam;

  constructor(context: FakeAudioContext) {
    super(context, "buffer-source");
    this.playbackRate = new FakeAudioParam(context, 1);
    this.detune = new FakeAudioParam(context, 0);
  }

  override start(when = 0): void {
    super.start(when);
    // A one-shot buffer ends by itself.
    const buffer = this.buffer;
    if (!this.loop && buffer !== null && this.startTime !== undefined) {
      this.stopTime = Math.min(this.stopTime, this.startTime + buffer.duration);
    }
  }

  override pitch(): FakeAudioParam {
    return this.playbackRate;
  }
}

export class FakeAudioContext implements AudioContextLike {
  readonly sampleRate = 48_000;
  readonly destination: FakeAudioNode;
  readonly nodes: FakeAudioNode[] = [];
  /** Sources in the order start() was called. */
  readonly started: FakeSourceNode[] = [];
  /** resume / suspend / close, in call order, with the audio time of the call. */
  readonly calls: { readonly name: "resume" | "suspend" | "close"; readonly atMs: number }[] = [];
  private clock = 0;
  private currentState: AudioContextState = "suspended";

  constructor() {
    this.destination = new FakeAudioNode(this, "destination");
  }

  get currentTime(): number {
    return this.clock;
  }

  get state(): AudioContextState {
    return this.currentState;
  }

  /** Moves the audio clock `ms` forward while running (a suspended or closed context does not advance). */
  advance(ms: number): void {
    if (this.currentState === "running") this.clock += ms / 1000;
  }

  createGain(): FakeGainNode {
    return this.track(new FakeGainNode(this));
  }

  createBiquadFilter(): FakeBiquadFilterNode {
    return this.track(new FakeBiquadFilterNode(this));
  }

  createOscillator(): FakeOscillatorNode {
    return this.track(new FakeOscillatorNode(this));
  }

  createBuffer(numberOfChannels: number, length: number, sampleRate: number): FakeAudioBuffer {
    return new FakeAudioBuffer(numberOfChannels, length, sampleRate);
  }

  createBufferSource(): FakeAudioBufferSourceNode {
    return this.track(new FakeAudioBufferSourceNode(this));
  }

  resume(): Promise<void> {
    this.calls.push({ name: "resume", atMs: this.clock * 1000 });
    if (this.currentState === "closed") return Promise.reject(new Error("InvalidStateError: closed"));
    this.currentState = "running";
    return Promise.resolve();
  }

  suspend(): Promise<void> {
    this.calls.push({ name: "suspend", atMs: this.clock * 1000 });
    if (this.currentState === "closed") return Promise.reject(new Error("InvalidStateError: closed"));
    this.currentState = "suspended";
    return Promise.resolve();
  }

  close(): Promise<void> {
    this.calls.push({ name: "close", atMs: this.clock * 1000 });
    this.currentState = "closed";
    return Promise.resolve();
  }

  /** The nodes connected directly to the destination. */
  intoDestination(): FakeAudioNode[] {
    return this.nodes.filter((node) => node.outputs.has(this.destination));
  }

  /**
   * The master gain (SPEC-0018 §6): the one node connected directly to the destination, which must be a GainNode.
   * Throws when the graph does not have that shape.
   */
  master(): FakeGainNode {
    const direct = this.intoDestination();
    const [only] = direct;
    if (direct.length !== 1 || !(only instanceof FakeGainNode)) {
      throw new Error(
        `expected exactly one GainNode connected to the destination (the master), found: ${direct.map((node) => node.kind).join(", ") || "none"}`,
      );
    }
    return only;
  }

  /** How loud `source` reaches the destination at audio time `t` (0 when it is not playing). */
  level(source: FakeSourceNode, t: number): number {
    if (!source.playingAt(t)) return 0;
    return this.pathGain(source, t, new Set());
  }

  /** Sum of the levels of every started source at audio time `t`. */
  output(t: number): number {
    return this.started.reduce((sum, source) => sum + this.level(source, t), 0);
  }

  private pathGain(node: FakeAudioNode, t: number, visiting: Set<FakeAudioNode>): number {
    if (node === this.destination) return 1;
    if (visiting.has(node)) return 0;
    visiting.add(node);
    let sum = 0;
    for (const next of node.outputs) sum += this.pathGain(next, t, visiting);
    visiting.delete(node);
    return node.gainAt(t) * sum;
  }

  private track<T extends FakeAudioNode>(node: T): T {
    this.nodes.push(node);
    return node;
  }
}
