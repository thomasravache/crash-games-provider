import { BetError, rechargeDemoWallet, WalletError } from "@apogeu/game-sdk";
import {
  BalanceDisplay,
  balanceText,
  betTestIds,
  type BalanceDisplayProps,
} from "@apogeu/game-ui";
import { shouldOfferRecharge } from "@apogeu/game-sdk/state";
import { describe, expect, test } from "vitest";

import { apiBaseUrl, limits, money, sessionToken } from "./support/bets";
import { startedRig } from "./support/bets-rig";
import { FakeWallet } from "./support/fake-wallet";
import { attributesOf, plainText, render, textOf } from "./support/react";

// SPEC-0026 UT-06: with balance 0,50 and minimum bet 1,00 "Recarregar demo" is shown; with balance 10,00 it is not;
// the recharge (POST /api/v1/wallet/demo/recharge, SPEC-0021@4) updates the balance with the server value. By the
// 2026-10-11 amendment the balance stays empty until the first read (no "R$ 0,00" and no false recharge).

function balanceMarkup(balance: string | null, recharging = false): string {
  const props: BalanceDisplayProps = {
    balance: balance === null ? null : money(balance),
    limits,
    recharging,
    onRecharge: () => undefined,
  };
  return render(BalanceDisplay, props);
}

describe("SPEC-0026 saldo demo e recarga", () => {
  test("SPEC-0026:UT-06 shouldOfferRecharge só com saldo abaixo da aposta mínima", () => {
    expect(shouldOfferRecharge(money("0.50"), limits)).toBe(true);
    expect(shouldOfferRecharge(money("0.99"), limits)).toBe(true);
    expect(shouldOfferRecharge(money("1.00"), limits)).toBe(false);
    expect(shouldOfferRecharge(money("10.00"), limits)).toBe(false);
    expect(shouldOfferRecharge(null, limits)).toBe(false);
    expect(shouldOfferRecharge(money("0.50"), null)).toBe(false);
  });

  test('SPEC-0026:UT-06 o saldo aparece como "R$ x · DEMO" e "Recarregar demo" aparece com 0,50 e some com 10,00', () => {
    const low = balanceMarkup("0.50");
    expect(textOf(low, betTestIds.balance)).toBe(balanceText.value("R$ 0,50"));
    expect(textOf(low, betTestIds.recharge)).toBe(balanceText.recharge);
    expect(attributesOf(low, betTestIds.recharge)).not.toMatch(/disabled/u);

    const enough = balanceMarkup("10.00");
    expect(textOf(enough, betTestIds.balance)).toBe(
      balanceText.value("R$ 10,00"),
    );
    expect(attributesOf(enough, betTestIds.recharge)).toBeNull();
    expect(plainText(enough)).not.toContain(balanceText.recharge);

    expect(textOf(balanceMarkup("2990.00"), betTestIds.balance)).toBe(
      "R$ 2.990,00 · DEMO",
    );
  });

  test("SPEC-0026:UT-06 antes da primeira leitura o saldo fica vazio, sem R$ 0,00 nem recarga; recarregando o botão fica desabilitado", () => {
    const empty = balanceMarkup(null);
    expect(plainText(empty)).not.toContain("R$ 0,00");
    expect(attributesOf(empty, betTestIds.recharge)).toBeNull();
    expect(textOf(empty, betTestIds.balance)).not.toMatch(/R\$/u);

    expect(
      attributesOf(balanceMarkup("0.50", true), betTestIds.recharge),
    ).toMatch(/disabled/u);
  });

  test("SPEC-0026:UT-06 a recarga atualiza o saldo com o valor do servidor; recusa mantém o saldo", async () => {
    const rig = await startedRig("0.50");
    expect(rig.state().balance).toBe("0.50");
    expect(shouldOfferRecharge(rig.state().balance, rig.state().limits)).toBe(
      true,
    );

    rig.wallet.rechargeTo = "3000.00";
    await rig.client.rechargeDemo();
    expect(rig.state().balance).toBe("3000.00");
    const [recharge] = rig.wallet.requestsTo("/api/v1/wallet/demo/recharge");
    expect(recharge).toMatchObject({
      method: "POST",
      authorization: `Bearer ${sessionToken}`,
    });
    expect(shouldOfferRecharge(rig.state().balance, rig.state().limits)).toBe(
      false,
    );

    rig.wallet.rechargeTo = null;
    const error = await rig.client.rechargeDemo().then(
      () => undefined,
      (reason: unknown) => reason,
    );
    expect(error).toBeInstanceOf(BetError);
    expect((error as BetError).code).toBe("RECHARGE_NOT_ALLOWED");
    expect(rig.state().balance).toBe("3000.00");
    await rig.client.stop();
  });

  test("SPEC-0026:UT-06 rechargeDemoWallet: 200 com o novo saldo; 409 vira WalletError RECHARGE_NOT_ALLOWED", async () => {
    const wallet = new FakeWallet("0.50");
    wallet.rechargeTo = "3000.00";
    await expect(
      rechargeDemoWallet(apiBaseUrl, sessionToken, wallet.fetch),
    ).resolves.toEqual({
      balance: "3000.00",
      currency: "BRL",
    });
    wallet.rechargeTo = null;
    const error = await rechargeDemoWallet(
      apiBaseUrl,
      sessionToken,
      wallet.fetch,
    ).then(
      () => undefined,
      (reason: unknown) => reason,
    );
    expect(error).toBeInstanceOf(WalletError);
    expect((error as WalletError).code).toBe("RECHARGE_NOT_ALLOWED");
  });
});
