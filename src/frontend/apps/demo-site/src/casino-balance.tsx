import { useEffect, useRef, useState } from "react";

import { readBalance, type CasinoBalance as Balance } from "./casino-api";
import { formatMoney } from "./money";

/** The top bar balance is read again every 5 s (SPEC-0047 §6). */
export const balanceRefreshMs = 5_000;

interface CasinoBalanceProps {
  /** Called when the casino session is gone (401). */
  readonly onUnauthorized: () => void;
}

/**
 * The balance of the casino wallet in the top bar (SPEC-0047 §6, Stitch "Cassino Demo — Lobby" and "Pipa (Refinado)"):
 * read when it appears (entering the lobby, back from the game, opening the game page) and every 5 s after that.
 */
export function CasinoBalance({ onUnauthorized }: CasinoBalanceProps) {
  const [balance, setBalance] = useState<Balance | null>(null);
  const unauthorized = useRef(onUnauthorized);

  useEffect(() => {
    unauthorized.current = onUnauthorized;
  }, [onUnauthorized]);

  useEffect(() => {
    let current: AbortController | null = null;
    const read = (): void => {
      current?.abort();
      const controller = new AbortController();
      current = controller;
      void readBalance(controller.signal).then((result) => {
        if (controller.signal.aborted) return;
        switch (result.status) {
          case "ok":
            setBalance(result.balance);
            break;
          case "unauthorized":
            unauthorized.current();
            break;
          case "unavailable":
            break;
        }
      });
    };
    read();
    const timer = window.setInterval(read, balanceRefreshMs);
    return () => {
      window.clearInterval(timer);
      current?.abort();
    };
  }, []);

  return (
    <p className="balance">
      <span className="balance-label">Saldo:</span>{" "}
      <span className="balance-amount">
        {balance === null ? "…" : formatMoney(balance.balance, balance.currency)}
      </span>
      <span className="balance-tag">Demo</span>
    </p>
  );
}
