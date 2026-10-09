// Recorders of what the real game-sdk sends and receives (SPEC-0010 CT-02, CT-03): a fetch wrapper for the REST calls
// and a RoundsHubConnection wrapper for the hub results and messages. They only observe; the payloads pass unchanged.
import type { RoundsHubConnection } from "@apogeu/game-sdk";

export interface HttpExchange {
  readonly url: string;
  readonly method: string;
  readonly requestBody: unknown;
  readonly status: number;
  readonly contentType: string;
  readonly responseBody: unknown;
}

export class HttpRecorder {
  readonly exchanges: HttpExchange[] = [];

  readonly fetch = async (input: string, init?: RequestInit): Promise<Response> => {
    const response = await fetch(input, init);
    const text = await response.clone().text();
    const requestBody = typeof init?.body === "string" ? parseJson(init.body) : undefined;
    this.exchanges.push({
      url: input,
      method: init?.method ?? "GET",
      requestBody,
      status: response.status,
      contentType: response.headers.get("content-type") ?? "",
      responseBody: text === "" ? undefined : parseJson(text),
    });
    return response;
  };
}

export interface HubRecord {
  /** "JoinSala" / "GetServerTime" (results) or the client method name of a server message. */
  readonly name: string;
  readonly kind: "result" | "message";
  readonly payload: unknown;
  readonly receivedAt: number;
}

export class HubRecorder {
  readonly records: HubRecord[] = [];

  wrap(inner: RoundsHubConnection): RoundsHubConnection {
    const record = (name: string, kind: HubRecord["kind"], payload: unknown): void => {
      this.records.push({ name, kind, payload: structuredClone(payload), receivedAt: Date.now() });
    };
    const invoke = inner.invoke.bind(inner) as (method: string, ...args: unknown[]) => Promise<unknown>;
    return {
      start: () => inner.start(),
      stop: () => inner.stop(),
      invoke: (async (method: string, ...args: unknown[]) => {
        const result = await invoke(method, ...args);
        record(method, "result", result);
        return result;
      }) as RoundsHubConnection["invoke"],
      on: (event, handler) => {
        const forward = handler as (message: unknown) => void;
        inner.on(event, ((message: unknown) => {
          record(event, "message", message);
          forward(message);
        }) as typeof handler);
      },
      onreconnecting: (callback) => {
        inner.onreconnecting(callback);
      },
      onreconnected: (callback) => {
        inner.onreconnected(callback);
      },
      onclose: (callback) => {
        inner.onclose(callback);
      },
    };
  }

  /** Index of the first record matching `name` (and round, when given) at or after `from`; −1 when absent. */
  indexOf(name: string, roundId?: string, from = 0): number {
    return this.records.findIndex(
      (entry, index) =>
        index >= from &&
        entry.name === name &&
        (roundId === undefined || (entry.payload as { roundId?: string }).roundId === roundId),
    );
  }
}

function parseJson(text: string): unknown {
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}
