// Texts and test ids of the HUD (SPEC-0010 §6, pt-BR). The E2E tests of tests/e2e/pipa find the HUD by these
// texts, roles and ids (ADR-0009: DOM, roles and labels, never pixels of the scene).

export const hudText = {
  betting: "PREPARE SUA PIPA",
  flying: "EM VOO",
  crashed: "PIPA CORTADA!",
  annulled: "Rodada anulada",
  reconnecting: "Reconectando…",
  demoBadge: "DEMO",
  betDisabled: "Apostas em breve",
  historyLabel: "Histórico de rodadas",
  fairnessTitle: "Comprovadamente Justo",
  verified: "Verificado",
  sessionExpired: "Sessão expirada. Volte ao cassino para jogar.",
  loading: "Carregando o jogo…",
  connectionFailed: "Não foi possível conectar ao jogo. Tente novamente em instantes.",
  waiting: "Aguardando a próxima rodada",
  balance: "Saldo demo",
  close: "Fechar",
  verifying: "Verificando…",
  chainMismatch: "Falhou: a semente não gera o hash da rodada anterior.",
  crashMismatch: "Falhou: o ponto de crash recalculado é diferente do informado.",
  verificationUnavailable: "Não foi possível obter os dados da rodada. Tente novamente.",
  pickRound: "Escolha uma rodada encerrada para conferir no seu navegador.",
} as const;

/** Announcements of the aria-live region: only phase changes, never each tick (ADR-0020). */
export const hudAnnouncement = {
  betting: (roundNumber: number | null) =>
    roundNumber === null
      ? "Nova rodada: prepare sua pipa."
      : `Rodada ${String(roundNumber)}: prepare sua pipa.`,
  flying: "A pipa está em voo.",
  crashed: (crashPoint: string) => `Pipa cortada em ${crashPoint}.`,
  annulled: "Rodada anulada.",
} as const;

export const hudTestIds = {
  /** Current multiplier while flying; the final crash point once crashed. */
  multiplier: "hud-multiplier",
  /** Seconds left of the betting phase. */
  countdown: "hud-countdown",
  /** Fixed demo balance. */
  balance: "hud-balance",
  /** Crash point shown by the "Comprovadamente Justo" window for the chosen round. */
  fairnessCrashPoint: "fairness-crash-point",
} as const;
