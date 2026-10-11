import { readFileSync } from "node:fs";

import {
  addMoney,
  cashOutPreview,
  compareMoney,
  formatMoney,
  isMoney,
  normalizeMoney,
  parseMoneyInput,
  parseMultiplierInput,
  subtractMoney,
  toMoney,
} from "@apogeu/game-sdk/state";
import { afterEach, describe, expect, test, vi } from "vitest";

import { bettingEvent } from "../support/rounds";
import { money, multiplier, sessionToken } from "./support/bets";
import { startedRig } from "./support/bets-rig";

// SPEC-0026 UT-04: "2990.5" and "24.70" are shown as "R$ 2.990,50" and "R$ 24,70" and travel as decimal strings,
// never through Number (ADR-0008). Also §5 (security): the session token never reaches browser storage.

const moneySource = readFileSync(
  new URL(
    "../../../../src/frontend/packages/game-sdk/src/state/money.ts",
    import.meta.url,
  ),
  "utf8",
);

describe("SPEC-0026 dinheiro em string", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  test('SPEC-0026:UT-04 formatMoney: "2990.5" → "R$ 2.990,50" e "24.70" → "R$ 24,70" (espaço comum, pt-BR)', () => {
    expect(formatMoney(money("2990.5"))).toBe("R$ 2.990,50");
    expect(formatMoney(money("24.70"))).toBe("R$ 24,70");
    expect(formatMoney(money("0"))).toBe("R$ 0,00");
    expect(formatMoney(money("10"))).toBe("R$ 10,00");
    expect(formatMoney(money("1000000.00"))).toBe("R$ 1.000.000,00");
    // Beyond the precision of a double: only exact decimal handling keeps every digit.
    expect(formatMoney(money("9007199254740993.01"))).toBe(
      "R$ 9.007.199.254.740.993,01",
    );
  });

  test("SPEC-0026:UT-04 normalização, leitura e comparação exatas", () => {
    expect(normalizeMoney(money("2990.5"))).toBe("2990.50");
    expect(normalizeMoney(money("10"))).toBe("10.00");
    expect(toMoney("24.70")).toBe("24.70");
    expect(() => toMoney("24.705")).toThrow(RangeError);
    expect(() => toMoney("abc")).toThrow(RangeError);
    expect(isMoney("10.5")).toBe(true);
    expect(isMoney("-1")).toBe(false);
    expect(parseMoneyInput("10,50")).toBe("10.50");
    expect(parseMoneyInput("10.50")).toBe("10.50");
    expect(parseMoneyInput("R$ 1.000,01")).toBe("1000.01");
    expect(parseMoneyInput("dez")).toBeNull();
    expect(parseMultiplierInput("2,5")).toBe("2.50");
    expect(parseMultiplierInput("2,50x")).toBe("2.50");
    expect(compareMoney(money("10.5"), money("10.50"))).toBe(0);
    expect(compareMoney(money("9.99"), money("10"))).toBe(-1);
    expect(addMoney(money("0.10"), money("0.20"))).toBe("0.30");
    expect(subtractMoney(money("10.00"), money("10.01"))).toBe("0.00");
  });

  test("SPEC-0026:UT-04 prévia do RETIRAR: floor(valor × m × 100)/100 exato e limitada pelo ganho máximo", () => {
    expect(cashOutPreview(money("10.00"), multiplier("2.47"))).toBe("24.70");
    // 10 × 1.13 is 11.299999… as a double: floor would give 11,29.
    expect(cashOutPreview(money("10.00"), multiplier("1.13"))).toBe("11.30");
    expect(cashOutPreview(money("0.33"), multiplier("1.01"))).toBe("0.33");
    expect(
      cashOutPreview(money("1000.00"), multiplier("50.00"), money("10000.00")),
    ).toBe("10000.00");
  });

  test("SPEC-0026:UT-04 money.ts não calcula com Number (sem Number, parseFloat, parseInt, Math nem toFixed)", () => {
    const code = moneySource
      .replace(/\/\/.*$/gmu, "")
      .replace(/\/\*[\s\S]*?\*\//gu, "");
    expect(code).not.toMatch(
      /\bNumber\s*\(|\bparseFloat\b|\bparseInt\b|\bMath\.|\.toFixed\s*\(|\bNumber\.parse/u,
    );
    expect(code).not.toMatch(/notImplemented/u);
  });

  test("SPEC-0026:UT-04 PlaceBet trafega valor e alvo como string decimal canônica; o token não vai para o armazenamento", async () => {
    const writes: string[] = [];
    const storage = {
      getItem: () => null,
      setItem: (key: string) => {
        writes.push(key);
      },
      removeItem: () => undefined,
      clear: () => undefined,
      key: () => null,
      length: 0,
    };
    vi.stubGlobal("localStorage", storage);
    vi.stubGlobal("sessionStorage", storage);

    const rig = await startedRig("3000.00");
    expect(rig.hub.hubUrl).toBe("http://api.test/hubs/bets");
    expect(rig.hub.options?.accessTokenFactory()).toBe(sessionToken);

    rig.round(bettingEvent(41, Date.parse("2026-10-11T12:00:06.000Z")));
    rig.hub.respond("PlaceBet", (request) =>
      Promise.resolve({
        betId: "bet-1",
        clientBetId: request.clientBetId,
        roundId: "round-41",
        roundNumber: 41,
        panel: request.panel,
        amount: request.amount,
        currency: "BRL",
        autoCashOut: request.autoCashOut,
        status: "placed",
        balance: money("2989.50"),
      }),
    );
    await rig.client.placeBet(1, {
      amount: money("10.5"),
      autoCashOut: multiplier("2.00"),
    });

    const [request] = rig.hub.calls("PlaceBet")[0] ?? [];
    expect(request).toEqual({
      clientBetId: "client-bet-1",
      panel: 1,
      amount: "10.50",
      autoCashOut: "2.00",
    });
    expect(JSON.stringify(request)).toContain('"amount":"10.50"');
    expect(rig.state().balance).toBe("2989.50");
    expect(writes).toEqual([]);
    await rig.client.stop();
  });
});
