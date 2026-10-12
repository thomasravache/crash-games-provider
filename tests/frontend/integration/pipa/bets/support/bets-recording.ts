// Recorder of what the real bets client of game-sdk sends to and receives from BetsHub (SPEC-0026 IT-01, CT-01): the
// requests and results of the server methods and the server messages. It only observes; payloads pass unchanged.
import type { BetsHubConnection } from "@apogeu/game-sdk";

import type { HubManifest } from "../../support/contracts";

export interface BetsHubRecord {
  /** Server method (PlaceBet, CashOut, …) or client message (CashedOut, BalanceUpdated, …). */
  readonly name: string;
  readonly kind: "request" | "result" | "message";
  readonly payload: unknown;
}

export class BetsHubRecorder {
  readonly records: BetsHubRecord[] = [];

  wrap(inner: BetsHubConnection): BetsHubConnection {
    const record = (
      name: string,
      kind: BetsHubRecord["kind"],
      payload: unknown,
    ): void => {
      this.records.push({ name, kind, payload: structuredClone(payload) });
    };
    const invoke = inner.invoke.bind(inner) as (
      method: string,
      ...args: unknown[]
    ) => Promise<unknown>;
    return {
      start: () => inner.start(),
      stop: () => inner.stop(),
      invoke: (async (method: string, ...args: unknown[]) => {
        if (args.length > 0) record(method, "request", args[0]);
        const result = await invoke(method, ...args);
        record(method, "result", result);
        return result;
      }) as BetsHubConnection["invoke"],
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

  named(name: string, kind?: BetsHubRecord["kind"]): BetsHubRecord[] {
    return this.records.filter(
      (entry) =>
        entry.name === name && (kind === undefined || entry.kind === kind),
    );
  }
}

/**
 * JSON Schema of a BetsHub record: the parameter of a client message, the request parameter or the result of a server
 * method. Throws when the hub manifest does not declare it (a message the contract does not publish is a break).
 */
export function betsHubSchema(
  manifest: HubManifest,
  record: BetsHubRecord,
): unknown {
  const hub = manifest.hubs.find((candidate) => candidate.name === "BetsHub");
  if (hub === undefined) throw new Error("BetsHub is not in the hub manifest");
  if (record.kind === "message") {
    const message = hub.client.find((method) => method.name === record.name);
    if (message === undefined)
      throw new Error(`BetsHub publishes no client message ${record.name}`);
    return message.parameters[0]?.schema;
  }
  const method = hub.server.find((candidate) => candidate.name === record.name);
  if (method === undefined)
    throw new Error(`BetsHub has no server method ${record.name}`);
  return record.kind === "result"
    ? method.returns
    : method.parameters[0]?.schema;
}

/** Integer cents of a decimal string ("2990.5" → 299050), for the balance arithmetic of the assertions. */
export function cents(value: unknown): bigint {
  if (typeof value !== "string" || !/^\d+(\.\d{1,2})?$/u.test(value)) {
    throw new Error(`not a decimal money string: ${JSON.stringify(value)}`);
  }
  const [units = "0", fraction = ""] = value.split(".");
  return BigInt(units) * 100n + BigInt(fraction.padEnd(2, "0"));
}
