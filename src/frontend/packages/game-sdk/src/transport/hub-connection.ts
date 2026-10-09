// Port of the real-time connection (ADR-0005): the client talks to this interface, so unit tests use a fake and the
// browser/Node use the SignalR adapter. Method names follow the SignalR HubConnection to keep the adapter thin.

import { NotImplementedError } from "../not-implemented";
import type { RoundsHubClient, RoundsHubServer } from "./wire";

export type HubServerMethod = keyof RoundsHubServer;
export type HubClientEvent = keyof RoundsHubClient;

export interface RoundsHubConnection {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke<M extends HubServerMethod>(
    method: M,
    ...args: Parameters<RoundsHubServer[M]>
  ): ReturnType<RoundsHubServer[M]>;
  on<E extends HubClientEvent>(event: E, handler: RoundsHubClient[E]): void;
  /** The transport dropped and automatic reconnection started. */
  onreconnecting(callback: (error?: Error) => void): void;
  /** Automatic reconnection succeeded (a new connection: the client must join the sala again). */
  onreconnected(callback: (connectionId?: string) => void): void;
  onclose(callback: (error?: Error) => void): void;
}

export interface HubConnectionOptions {
  /** Hub authentication: access_token = sessionToken (SPEC-0009@1). */
  readonly accessTokenFactory: () => string;
}

/** Creates a connection to `/hubs/rounds`. */
export type ConnectHub = (hubUrl: string, options: HubConnectionOptions) => RoundsHubConnection;

/** SignalR adapter with automatic reconnection (browser and Node). */
export const createSignalRConnection: ConnectHub = (hubUrl, options) => {
  throw new NotImplementedError("createSignalRConnection", hubUrl, options);
};
