// Server error codes of the bet panel and their pt-BR messages (SPEC-0026 §6, UT-05). The codes come from the hub
// /hubs/bets (SPEC-0022@3, SPEC-0023@2) and the demo wallet (SPEC-0021@4); anything else is UNKNOWN.

import { notImplemented } from "../not-implemented";

/** The codes listed in the SPEC-0026 §6 contract. */
export const contractBetErrorCodes = [
  "BET_BELOW_MIN",
  "BET_ABOVE_MAX",
  "INSUFFICIENT_FUNDS",
  "PANEL_BUSY",
  "CANCEL_NOT_ALLOWED",
  "CASHOUT_TOO_LATE",
  "WALLET_UNAVAILABLE",
  "INVALID_PANEL",
  "ROUND_CLOSED",
  "BET_NOT_ACTIVE",
  "REAL_MONEY_DISABLED",
] as const;

/**
 * Every code the panel may receive: the SPEC-0026 list, the other hub codes of the consumed contracts
 * (INVALID_AMOUNT and AUTO_CASHOUT_INVALID of SPEC-0022@3, BET_NOT_FOUND of SPEC-0022@3/SPEC-0023@2), the demo
 * recharge codes (SPEC-0021@4) and the client-side NETWORK and UNKNOWN.
 */
export const betErrorCodes = [
  ...contractBetErrorCodes,
  "INVALID_AMOUNT",
  "AUTO_CASHOUT_INVALID",
  "BET_NOT_FOUND",
  "RECHARGE_NOT_ALLOWED",
  "NOT_DEMO",
  "NETWORK",
  "UNKNOWN",
] as const;

export type BetErrorCode = (typeof betErrorCodes)[number];

/** pt-BR message of each code, shown in the panel (role="alert"). */
export const betErrorMessages: Readonly<Record<BetErrorCode, string>> = {
  BET_BELOW_MIN: "Aposta abaixo do valor mínimo.",
  BET_ABOVE_MAX: "Aposta acima do valor máximo.",
  INSUFFICIENT_FUNDS: "Saldo insuficiente para esta aposta.",
  PANEL_BUSY: "Este painel já tem uma aposta em andamento.",
  CANCEL_NOT_ALLOWED: "Não é mais possível cancelar: a pipa já decolou.",
  CASHOUT_TOO_LATE: "Cash-out tardio: a pipa já tinha sido cortada",
  WALLET_UNAVAILABLE: "Carteira indisponível no momento. Tente novamente.",
  INVALID_PANEL: "Painel de aposta inválido.",
  ROUND_CLOSED: "A rodada fechou antes da confirmação; o valor foi estornado.",
  BET_NOT_ACTIVE: "A aposta não está em voo.",
  REAL_MONEY_DISABLED: "Apostas com dinheiro real estão desativadas.",
  INVALID_AMOUNT: "Valor de aposta inválido.",
  AUTO_CASHOUT_INVALID: "Alvo do cash-out automático inválido.",
  BET_NOT_FOUND: "Aposta não encontrada.",
  RECHARGE_NOT_ALLOWED: "A recarga só é possível com saldo abaixo da aposta mínima.",
  NOT_DEMO: "A recarga só existe no modo demo.",
  NETWORK: "Sem conexão com o servidor. Tente novamente.",
  UNKNOWN: "Algo deu errado. Tente novamente.",
};

export function isBetErrorCode(value: unknown): value is BetErrorCode {
  return notImplemented(value);
}

/** Known code as is; anything else (or nothing) as UNKNOWN. */
export function toBetErrorCode(value: unknown): BetErrorCode {
  return notImplemented(value);
}

/** pt-BR message of a code; an unknown code gets the UNKNOWN message. */
export function betErrorMessage(code: string): string {
  return notImplemented(code);
}
