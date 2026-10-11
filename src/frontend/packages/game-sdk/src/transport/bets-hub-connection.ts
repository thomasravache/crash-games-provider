// Port of the bets hub connection (/hubs/bets, SPEC-0022@3, SPEC-0023@2, SPEC-0024@2), like RoundsHubConnection:
// the bets client talks to this interface, so unit tests use a fake and the browser/Node use the SignalR adapter.

import { notImplemented } from "../not-implemented";
import type { HubConnectionOptions } from "./hub-connection";
import type { BetsHubClientEvents, BetsHubServer } from "./wire";

export type BetsHubServerMethod = keyof BetsHubServer;
export type BetsHubClientEvent = keyof BetsHubClientEvents;

export interface BetsHubConnection {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke<M extends BetsHubServerMethod>(
    method: M,
    ...args: Parameters<BetsHubServer[M]>
  ): ReturnType<BetsHubServer[M]>;
  on<E extends BetsHubClientEvent>(event: E, handler: BetsHubClientEvents[E]): void;
  /** The transport dropped and automatic reconnection started. */
  onreconnecting(callback: (error?: Error) => void): void;
  /** Automatic reconnection succeeded: the bets client reloads the balance and the open bets (UT-03). */
  onreconnected(callback: (connectionId?: string) => void): void;
  onclose(callback: (error?: Error) => void): void;
}

/** Creates a connection to `/hubs/bets`. */
export type ConnectBetsHub = (hubUrl: string, options: HubConnectionOptions) => BetsHubConnection;

/** SignalR adapter of /hubs/bets with automatic reconnection (reconnectDelayMs), browser and Node. */
export const createSignalRBetsConnection: ConnectBetsHub = (hubUrl, options) =>
  notImplemented(hubUrl, options);

/**
 * Error code of a failed hub invocation: the server throws HubException with the code as its message, which the
 * SignalR client wraps ("… HubException: BET_BELOW_MIN"). Undefined when there is no code (network, unknown).
 */
export function hubErrorCode(error: unknown): string | undefined {
  return notImplemented(error);
}
