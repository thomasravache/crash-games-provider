import { betsTabsTestIds, betsTabsText, MyBetsTab } from "@apogeu/game-ui";
import { describe, expect, test, vi } from "vitest";

import { attributesOf, render, textOf } from "../bets/support/react";
import { myBet, newTabsRig, sessionToken, type TabsRig } from "./support/tabs";

// SPEC-0031 UT-02: two pages of GET /api/v1/me/bets (SPEC-0025@4, newest first, opaque cursor). "Carregar mais" reads
// the second page by the cursor and appends it without duplicating a bet; on the last page (no nextCursor) the button
// is gone and nothing else is requested. The statuses cashed_out, lost, cancelled and refunded show in the rows.

const bet3 = myBet({
  betId: "bet-3",
  roundNumber: 43,
  amount: "5.00",
  status: "cancelled",
  placedAt: "2026-10-15T12:03:00.000Z",
});

const page1 = [
  myBet({
    betId: "bet-5",
    roundNumber: 45,
    amount: "10.00",
    status: "cashed_out",
    multiplier: "2.47",
    payout: "24.70",
    placedAt: "2026-10-15T12:05:00.000Z",
  }),
  myBet({
    betId: "bet-4",
    roundNumber: 44,
    amount: "10.00",
    status: "lost",
    placedAt: "2026-10-15T12:04:00.000Z",
  }),
  bet3,
];

// The second page repeats bet-3 (the boundary of the cursor): it must not show twice.
const page2 = [
  bet3,
  myBet({
    betId: "bet-2",
    roundNumber: 42,
    amount: "7.50",
    status: "refunded",
    placedAt: "2026-10-15T12:02:00.000Z",
  }),
  myBet({
    betId: "bet-1",
    roundNumber: 41,
    amount: "1.00",
    status: "lost",
    placedAt: "2026-10-15T12:01:00.000Z",
  }),
];

async function twoPagesRig(): Promise<TabsRig> {
  const rig = newTabsRig();
  rig.history.page(null, page1, "cursor-after-bet-3");
  rig.history.page("cursor-after-bet-3", page2, null);
  await rig.client.start(sessionToken);
  return rig;
}

const ids = (rig: TabsRig): string[] =>
  rig.mine().items.map((item) => item.betId);

const renderMine = (rig: TabsRig): string =>
  render(MyBetsTab, {
    state: rig.mine(),
    onLoadMore: () => {
      void rig.client.loadMoreMyBets();
    },
  });

describe("SPEC-0031 aba Minhas apostas", () => {
  test("SPEC-0031:UT-02 a primeira página lista as apostas do jogador e oferece Carregar mais", async () => {
    const rig = await twoPagesRig();

    await rig.client.loadMyBets();

    const [first] = rig.history.requests;
    expect(rig.history.requests).toHaveLength(1);
    expect(first?.url.searchParams.has("before")).toBe(false);
    expect(first?.authorization).toBe(`Bearer ${sessionToken}`);
    expect(ids(rig)).toEqual(["bet-5", "bet-4", "bet-3"]);
    expect(rig.mine()).toMatchObject({
      nextCursor: "cursor-after-bet-3",
      loaded: true,
      loading: false,
      error: null,
    });

    const markup = renderMine(rig);
    const won = textOf(markup, betsTabsTestIds.myBet("bet-5")) ?? "";
    expect(won).toContain(betsTabsText.status.cashed_out);
    expect(won).toContain("R$ 24,70");
    expect(won).toContain("2,47x");
    expect(textOf(markup, betsTabsTestIds.myBet("bet-4"))).toContain(
      betsTabsText.status.lost,
    );
    expect(textOf(markup, betsTabsTestIds.myBet("bet-3"))).toContain(
      betsTabsText.status.cancelled,
    );
    expect(textOf(markup, betsTabsTestIds.loadMore)).toBe(
      betsTabsText.loadMore,
    );
    expect(attributesOf(markup, betsTabsTestIds.loadMore)).not.toContain(
      "disabled",
    );
    await rig.client.stop();
  });

  test("SPEC-0031:UT-02 Carregar mais anexa a segunda página pelo cursor, sem duplicar, e o botão some na última página", async () => {
    const rig = await twoPagesRig();
    await rig.client.loadMyBets();

    await rig.client.loadMoreMyBets();

    expect(rig.history.requests).toHaveLength(2);
    const [first, second] = rig.history.requests;
    expect(second?.url.searchParams.get("before")).toBe("cursor-after-bet-3");
    expect(second?.url.searchParams.get("limit")).toBe(
      first?.url.searchParams.get("limit") ?? null,
    );
    expect(second?.authorization).toBe(`Bearer ${sessionToken}`);
    expect(ids(rig)).toEqual(["bet-5", "bet-4", "bet-3", "bet-2", "bet-1"]);
    expect(rig.mine().nextCursor).toBeNull();

    const markup = renderMine(rig);
    expect(textOf(markup, betsTabsTestIds.myBet("bet-2"))).toContain(
      betsTabsText.status.refunded,
    );
    expect(
      markup.split(`data-testid="${betsTabsTestIds.myBet("bet-3")}"`),
    ).toHaveLength(2);
    expect(attributesOf(markup, betsTabsTestIds.loadMore)).toBeNull();

    // On the last page "carregar mais" requests nothing else.
    await rig.client.loadMoreMyBets();
    expect(rig.history.requests).toHaveLength(2);
    expect(ids(rig)).toHaveLength(5);
    await rig.client.stop();
  });

  test("SPEC-0031:UT-02 dois cliques em Carregar mais com a página a caminho pedem a segunda página uma vez só", async () => {
    const rig = await twoPagesRig();
    await rig.client.loadMyBets();

    rig.history.hold = true;
    const clicks = [rig.client.loadMoreMyBets(), rig.client.loadMoreMyBets()];
    await vi.waitFor(
      () => {
        expect(rig.history.requests).toHaveLength(2);
      },
      { timeout: 1_000, interval: 5 },
    );
    expect(rig.mine().loading).toBe(true);
    rig.history.hold = false;
    rig.history.release();
    await Promise.all(clicks);

    expect(
      rig.history.requests.filter(
        (request) =>
          request.url.searchParams.get("before") === "cursor-after-bet-3",
      ),
    ).toHaveLength(1);
    expect(ids(rig)).toEqual(["bet-5", "bet-4", "bet-3", "bet-2", "bet-1"]);
    await rig.client.stop();
  });
});
