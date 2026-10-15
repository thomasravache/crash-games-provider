// Texts and test ids of the bets tabs below the panels (SPEC-0031, pt-BR, Stitch "Pipa Crash"): "Todas as apostas"
// (live bets of the round, SPEC-0025@4) and "Minhas apostas" (the player's history). The tests of
// tests/frontend/pipa/bets-tabs and tests/e2e/pipa/bets-tabs find the tabs by these roles, texts and ids (ADR-0009).

import type { MyBetStatus } from "@apogeu/game-sdk/state";

export const betsTabsText = {
  /** Tab (role "tab") and panel name of the live bets. */
  all: "Todas as apostas",
  /** Tab (role "tab") and panel name of the player's history. */
  mine: "Minhas apostas",
  /** Accessible name of the tab list. */
  tabList: "Apostas",
  /** Button of the next history page. */
  loadMore: "Carregar mais",
  /** Column headers of "Todas as apostas". */
  liveColumns: { player: "Jogador", amount: "Aposta", multiplier: "Mult.", payout: "Ganho" },
  /** Column headers of "Minhas apostas". */
  mineColumns: {
    round: "Rodada",
    amount: "Aposta",
    multiplier: "Mult.",
    payout: "Ganho",
    status: "Situação",
  },
  /** Totals of the round in "Todas as apostas". */
  totals: { bets: "Apostas", players: "Jogadores", wagered: "Total apostado" },
  /** Round number in a row of "Minhas apostas": "#45". */
  round: (roundNumber: number | null) => (roundNumber === null ? "—" : `#${String(roundNumber)}`),
  /** Empty value (open bet, no multiplier or payout). */
  none: "—",
  liveEmpty: "Nenhuma aposta nesta rodada ainda.",
  mineEmpty: "Você ainda não fez apostas.",
  mineLoading: "Carregando suas apostas…",
  mineError: "Não foi possível carregar suas apostas. Tente de novo.",
  retry: "Tentar de novo",
  /** Status of a bet in "Minhas apostas". */
  status: {
    placed: "Em jogo",
    queued: "Próxima rodada",
    active: "Em jogo",
    cashed_out: "Ganhou",
    lost: "Perdeu",
    cancelled: "Cancelada",
    refunded: "Estornada",
  } satisfies Record<MyBetStatus, string>,
} as const;

export const betsTabsTestIds = {
  /** Row of a live bet, by its opaque betRef. */
  liveBet: (betRef: string) => `live-bet-${betRef}`,
  liveTotalBets: "live-total-bets",
  liveTotalPlayers: "live-total-players",
  liveTotalWagered: "live-total-wagered",
  /** The tabs ("all" / "mine"). */
  tab: (tab: "all" | "mine") => `bets-tab-${tab}`,
  /** Row of a bet of the player, by its betId. */
  myBet: (betId: string) => `my-bet-${betId}`,
  loadMore: "my-bets-load-more",
} as const;
