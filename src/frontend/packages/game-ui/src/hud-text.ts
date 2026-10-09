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
