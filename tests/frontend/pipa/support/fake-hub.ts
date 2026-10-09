// Fake RoundsHub connection (SPEC-0010 §7: test doubles built from the SPEC-0008@2 contract). It implements the
// RoundsHubConnection port of game-sdk, answers JoinSala/GetServerTime from queues and lets the test emit server
// messages and simulate the drop and return of the transport.
import type {
  HubClientEvent,
  HubConnectionOptions,
  RoundsHubClient,
  RoundsHubConnection,
  RoundSnapshot,
} from "@apogeu/game-sdk";

type Handler = (message: never) => void;

export class FakeRoundsHub implements RoundsHubConnection {
  readonly invocations: { method: string; args: unknown[] }[] = [];
  started = false;
  hubUrl: string | null = null;
  options: HubConnectionOptions | null = null;

  private readonly handlers = new Map<string, Handler[]>();
  private readonly reconnecting: ((error?: Error) => void)[] = [];
  private readonly reconnected: ((connectionId?: string) => void)[] = [];
  private readonly closed: ((error?: Error) => void)[] = [];
  private readonly snapshots: RoundSnapshot[] = [];

  constructor(private readonly serverClock: () => number) {}

  /** Next JoinSala results, in order (the last one repeats). */
  queueSnapshot(snapshot: RoundSnapshot): void {
    this.snapshots.push(snapshot);
  }

  start(): Promise<void> {
    this.started = true;
    return Promise.resolve();
  }

  stop(): Promise<void> {
    this.started = false;
    return Promise.resolve();
  }

  invoke: RoundsHubConnection["invoke"] = ((method: string, ...args: unknown[]) => {
    this.invocations.push({ method, args });
    if (method === "GetServerTime") {
      return Promise.resolve({ serverTime: new Date(this.serverClock()).toISOString() });
    }
    if (method === "JoinSala") {
      const snapshot = this.snapshots.length > 1 ? this.snapshots.shift() : this.snapshots[0];
      return snapshot === undefined
        ? Promise.reject(new Error("FakeRoundsHub: no snapshot queued for JoinSala"))
        : Promise.resolve(structuredClone(snapshot));
    }
    return Promise.reject(new Error(`FakeRoundsHub: unexpected method ${method}`));
  }) as RoundsHubConnection["invoke"];

  on<E extends HubClientEvent>(event: E, handler: RoundsHubClient[E]): void {
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

  /** The server sends `event` to the sala group. */
  emit<E extends HubClientEvent>(event: E, message: Parameters<RoundsHubClient[E]>[0]): void {
    for (const handler of this.handlers.get(event) ?? []) {
      (handler as (payload: typeof message) => void)(message);
    }
  }

  /** The transport drops: automatic reconnection starts. */
  dropConnection(): void {
    for (const callback of this.reconnecting) callback(new Error("transport lost (test)"));
  }

  /** Automatic reconnection succeeded with a new connection id. */
  restoreConnection(): void {
    for (const callback of this.reconnected) callback("connection-2");
  }

  joinSalaCalls(): unknown[][] {
    return this.invocations.filter((call) => call.method === "JoinSala").map((call) => call.args);
  }
}

/** fetch double answering POST {api}/api/v1/sessions with the given body. */
export function fakeSessionFetch(
  apiBaseUrl: string,
  body: unknown,
): {
  fetch: (input: string, init?: RequestInit) => Promise<Response>;
  requests: { url: string; init?: RequestInit }[];
} {
  const requests: { url: string; init?: RequestInit }[] = [];
  return {
    requests,
    fetch: (input, init) => {
      requests.push(init === undefined ? { url: input } : { url: input, init });
      if (input === `${apiBaseUrl}/api/v1/sessions` && init?.method === "POST") {
        return Promise.resolve(
          new Response(JSON.stringify(body), {
            status: 200,
            headers: { "content-type": "application/json" },
          }),
        );
      }
      return Promise.resolve(new Response(null, { status: 404 }));
    },
  };
}
