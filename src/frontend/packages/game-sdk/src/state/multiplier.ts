// Client-side interpolation of the multiplier (ADR-0019, SPEC-0008@2 §4): m(t) = e^(k·t), t in ms since the flight
// started on the server clock, shown as floor(m·100)/100. Display only: the crash point always comes from the server.

import { NotImplementedError } from "../not-implemented";

export interface FlightClock {
  /** Flight start on the server clock (epoch ms). */
  readonly startedAtMs: number;
  /** Growth constant k per millisecond. */
  readonly growthK: number;
  /** serverClock − localClock in ms (RoundState.serverOffsetMs). */
  readonly serverOffsetMs: number;
}

/** Multiplier shown at the local instant `localNowMs`: floor(e^(k·(localNowMs + offset − startedAt))·100)/100, ≥ 1. */
export function multiplierAt(clock: FlightClock, localNowMs: number): number {
  throw new NotImplementedError("multiplierAt", clock, localNowMs);
}

/** One GetServerTime round trip, measured on the local clock (epoch ms). */
export interface ServerTimeSample {
  readonly requestSentAtMs: number;
  readonly responseReceivedAtMs: number;
  /** ISO-8601 UTC `serverTime` returned by GetServerTime. */
  readonly serverTime: string;
}

/** serverClock − localClock estimated from one sample (server time against the midpoint of the round trip). */
export function estimateServerOffset(sample: ServerTimeSample): number {
  throw new NotImplementedError("estimateServerOffset", sample);
}
