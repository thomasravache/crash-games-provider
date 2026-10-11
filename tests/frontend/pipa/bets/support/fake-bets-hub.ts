// Fake BetsHub connection (SPEC-0026 §7: test doubles built from the SPEC-0022@3, SPEC-0023@2 and SPEC-0024@2
// contracts). It implements the BetsHubConnection port of game-sdk, answers the server methods with responders the test
// sets, lets the test emit server messages and simulate the drop and return of the transport.
import type {
  BetsHubClientEvent,
  BetsHubClientEvents,
  BetsHubConnection,
  BetsHubServer,
  BetsHubServerMethod,
  HubConnectionOptions,
} from "@apogeu/game-sdk";
import type { BetLimits } from "@apogeu/game-sdk/state";

type Handler = (message: never) => void;
type Responder = (...args: never[]) => Promise<unknown>;

/** The error the SignalR client raises for a HubException thrown with `code` by the server. */
export function hubError(method: string, code: string): Error {
  return new Error(
    `An unexpected error occurred invoking '${method}' on the server. HubException: ${code}`,
  );
}

export class FakeBetsHub implements BetsHubConnection {
  readonly invocations: { method: string; args: unknown[] }[] = [];
  started = false;
  hubUrl: string | null = null;
  options: HubConnectionOptions | null = null;

  private readonly handlers = new Map<string, Handler[]>();
  private readonly responders = new Map<string, Responder>();
  private readonly reconnecting: ((error?: Error) => void)[] = [];
  private readonly reconnected: ((connectionId?: string) => void)[] = [];
  private readonly closed: ((error?: Error) => void)[] = [];

  constructor(limits: BetLimits) {
    this.respond("GetBetLimits", () =>
      Promise.resolve(structuredClone(limits)),
    );
  }

  /** How the server answers `method` from now on. */
  respond<M extends BetsHubServerMethod>(
    method: M,
    responder: (
      ...args: Parameters<BetsHubServer[M]>
    ) => ReturnType<BetsHubServer[M]>,
  ): void {
    this.responders.set(method, responder as unknown as Responder);
  }

  start(): Promise<void> {
    this.started = true;
    return Promise.resolve();
  }

  stop(): Promise<void> {
    this.started = false;
    return Promise.resolve();
  }

  invoke: BetsHubConnection["invoke"] = ((
    method: string,
    ...args: unknown[]
  ) => {
    this.invocations.push({ method, args: structuredClone(args) });
    const responder = this.responders.get(method);
    if (responder === undefined) {
      return Promise.reject(
        new Error(`FakeBetsHub: no responder for ${method}`),
      );
    }
    return (responder as (...values: unknown[]) => Promise<unknown>)(...args);
  }) as BetsHubConnection["invoke"];

  on<E extends BetsHubClientEvent>(
    event: E,
    handler: BetsHubClientEvents[E],
  ): void {
    const list = this.handlers.get(event) ?? [];
    list.push(handler as Handler);
    this.handlers.set(event, list);
  }

  onreconnecting(callback: (error?: Error) => void): void {
    this.reconnecting.push(callback);
  }

  onreconnected(callback: (connectionId?: string) => void): void {
    this.reconnected.push(callback);
  }

  onclose(callback: (error?: Error) => void): void {
    this.closed.push(callback);
  }

  /** Whether the client subscribed to `event`. */
  listens(event: BetsHubClientEvent): boolean {
    return (this.handlers.get(event)?.length ?? 0) > 0;
  }

  /** The server sends `event` to the player. */
  emit<E extends BetsHubClientEvent>(
    event: E,
    message: Parameters<BetsHubClientEvents[E]>[0],
  ): void {
    for (const handler of this.handlers.get(event) ?? []) {
      (handler as (payload: typeof message) => void)(structuredClone(message));
    }
  }

  dropConnection(): void {
    for (const callback of this.reconnecting)
      callback(new Error("transport lost (test)"));
  }

  restoreConnection(): void {
    for (const callback of this.reconnected) callback("connection-2");
  }

  calls(method: BetsHubServerMethod): unknown[][] {
    return this.invocations
      .filter((call) => call.method === method)
      .map((call) => call.args);
  }
}
