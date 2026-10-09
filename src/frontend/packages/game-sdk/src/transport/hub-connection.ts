// Port of the real-time connection (ADR-0005): the client talks to this interface, so unit tests use a fake and the
// browser/Node use the SignalR adapter. Method names follow the SignalR HubConnection to keep the adapter thin.

import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

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

/** Delay before reconnection attempt n (0, 1 s, 2 s, 4 s, then every 5 s): never gives up while the page is open. */
export function reconnectDelayMs(previousRetryCount: number): number {
  return previousRetryCount === 0 ? 0 : Math.min(1_000 * 2 ** (previousRetryCount - 1), 5_000);
}

/** SignalR adapter with automatic reconnection (browser and Node). */
export const createSignalRConnection: ConnectHub = (hubUrl, options) => {
  const hub = new HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: options.accessTokenFactory,
      // The session travels as access_token, never as a cookie: the game runs in a casino iframe (SPEC-0009@1).
      withCredentials: false,
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (context) => reconnectDelayMs(context.previousRetryCount),
    })
    // Nothing of the session reaches the browser console (SPEC-0010 §5).
    .configureLogging(LogLevel.None)
    .build();

  const invoke = (method: string, ...args: unknown[]): Promise<unknown> =>
    hub.invoke(method, ...args);

  return {
    start: () => hub.start(),
    stop: () => hub.stop(),
    invoke: invoke as RoundsHubConnection["invoke"],
    on(event, handler) {
      hub.on(event, handler as (...args: unknown[]) => void);
    },
    onreconnecting(callback) {
      hub.onreconnecting(callback);
    },
    onreconnected(callback) {
      hub.onreconnected(callback);
    },
    onclose(callback) {
      hub.onclose(callback);
    },
  };
};
