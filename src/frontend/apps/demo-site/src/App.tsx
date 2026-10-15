import { useEffect, useState } from "react";

import { currentPlayer, leaveCasino, type CasinoPlayer } from "./casino-api";
import { EntryScreen } from "./entry-screen";
import { LobbyScreen } from "./lobby-screen";

type CasinoState =
  | { readonly status: "loading" }
  | { readonly status: "anonymous" }
  | { readonly status: "playing"; readonly player: CasinoPlayer };

export function App() {
  const [state, setState] = useState<CasinoState>({ status: "loading" });

  // An existing casino session (the HttpOnly cookie) goes straight to the lobby.
  useEffect(() => {
    const controller = new AbortController();
    currentPlayer(controller.signal)
      .then((player) => {
        setState((current) =>
          current.status !== "loading"
            ? current
            : player === null
              ? { status: "anonymous" }
              : { status: "playing", player },
        );
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setState((current) => (current.status === "loading" ? { status: "anonymous" } : current));
        }
      });
    return () => {
      controller.abort();
    };
  }, []);

  switch (state.status) {
    case "loading":
      return (
        <main className="entry" aria-busy="true">
          <p className="lead">Carregando…</p>
        </main>
      );
    case "anonymous":
      return (
        <EntryScreen
          onEntered={(player) => {
            setState({ status: "playing", player });
          }}
        />
      );
    case "playing":
      return (
        <LobbyScreen
          player={state.player}
          onLeave={() => {
            void leaveCasino()
              .catch(() => undefined)
              .then(() => {
                setState({ status: "anonymous" });
              });
          }}
        />
      );
  }
}
