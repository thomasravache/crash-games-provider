// Texts, announcements and test ids of the bet panel and the balance (SPEC-0026 §6, pt-BR, Stitch "Pipa Crash"). The
// E2E tests of tests/e2e/pipa/bets find the panel by these texts, roles and ids (ADR-0009).

import type { PanelNumber } from "@apogeu/game-sdk/state";

export const betPanelText = {
  /** Heading of each panel: "APOSTA 1", "APOSTA 2". */
  title: (panel: PanelNumber) => `APOSTA ${String(panel)}`,
  /** Green button while idle, with the formatted amount: "APOSTAR R$ 10,00". */
  bet: (formattedAmount: string) => `APOSTAR ${formattedAmount}`,
  cancel: "CANCELAR",
  /** Queued bet (placed during the flight): "PRÓXIMA" next to "CANCELAR". */
  next: "PRÓXIMA",
  /** Orange button while active, with the formatted payout preview: "RETIRAR R$ 24,70". */
  cashOut: (formattedPayout: string) => `RETIRAR ${formattedPayout}`,
  cashingOut: "Retirando…",
  won: (formattedPayout: string) => `Ganhou ${formattedPayout}`,
  lost: "Perdeu",
  refunded: (formattedAmount: string) => `Estornado ${formattedAmount}`,
  /** Cash-out or refund whose credit is not confirmed yet (creditStatus "pending"). */
  creditPending: "Crédito em processamento",
  amount: "Valor da aposta",
  decrease: "Diminuir R$ 1,00",
  increase: "Aumentar R$ 1,00",
  /** Quick chip: sets the amount ("R$ 10"). */
  chip: (formattedAmount: string) => `Valor ${formattedAmount}`,
  autoCashOut: "Cash-out automático",
  autoTarget: "Alvo do cash-out automático",
} as const;

export const balanceText = {
  /** Balance with the demo badge: "R$ 2.990,00 · DEMO". */
  value: (formattedBalance: string) => `${formattedBalance} · DEMO`,
  label: "Saldo demo",
  demoBadge: "DEMO",
  recharge: "Recarregar demo",
  loading: "—",
} as const;

/** aria-live announcements of the panel: bet accepted, cash-out and loss (WCAG 2.2 AA, SPEC-0026 §2). */
export const betAnnouncement = {
  accepted: (panel: PanelNumber, formattedAmount: string) =>
    `Aposta ${String(panel)} de ${formattedAmount} aceita.`,
  queued: (panel: PanelNumber, formattedAmount: string) =>
    `Aposta ${String(panel)} de ${formattedAmount} na próxima rodada.`,
  cashedOut: (panel: PanelNumber, formattedPayout: string, multiplier: string) =>
    `Aposta ${String(panel)}: retirou ${formattedPayout} em ${multiplier}.`,
  autoCashedOut: (panel: PanelNumber, formattedPayout: string, multiplier: string) =>
    `Aposta ${String(panel)}: retirada automática de ${formattedPayout} em ${multiplier}.`,
  lost: (panel: PanelNumber) => `Aposta ${String(panel)} perdida.`,
  refunded: (panel: PanelNumber, formattedAmount: string) =>
    `Aposta ${String(panel)} estornada: ${formattedAmount}.`,
} as const;

export const betTestIds = {
  panel: (panel: PanelNumber) => `bet-panel-${String(panel)}`,
  amount: (panel: PanelNumber) => `bet-amount-${String(panel)}`,
  action: (panel: PanelNumber) => `bet-action-${String(panel)}`,
  result: (panel: PanelNumber) => `bet-result-${String(panel)}`,
  error: (panel: PanelNumber) => `bet-error-${String(panel)}`,
  disabledReason: (panel: PanelNumber) => `bet-reason-${String(panel)}`,
  /** The confirmed balance (same id as the fixed balance of SPEC-0010, which it replaces). */
  balance: "hud-balance",
  recharge: "bets-recharge",
} as const;
