import { useCallback, useEffect, useRef, useState } from "react";

import { currentPlayer, leaveCasino, playGame, type CasinoPlayer } from "./casino-api";
import { EntryScreen } from "./entry-screen";
import { GameScreen } from "./game-screen";
import { LobbyScreen } from "./lobby-screen";
import { gamePath, navigate, routeOf, type Route } from "./routes";

type CasinoState =
  | { readonly status: "loading" }
  | { readonly status: "anonymous" }
  | { readonly status: "playing"; readonly player: CasinoPlayer };

/** A launch of the Apogeu (SPEC-0045 §6): single use, so it lives only while its game page is open. */
interface Launch {
  readonly code: string;
  readonly launchUrl: string;
}

const notAvailableText = "Este jogo não está disponível no momento. Escolha outro jogo no lobby.";
const unavailableText = "Não foi possível abrir o jogo agora. Tente de novo em instantes.";

function currentRoute(): Route {
  return routeOf(window.location.pathname);
}

export function App() {
  const [state, setState] = useState<CasinoState>({ status: "loading" });
  const [route, setRoute] = useState<Route>(currentRoute);
  const [launch, setLaunch] = useState<Launch | null>(null);
  const [opening, setOpening] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const inFlight = useRef<string | null>(null);

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

  // Browser back/forward: leaving a game page drops its launch, so coming back to it launches again.
  useEffect(() => {
    const onPopState = (): void => {
      const next = currentRoute();
      setRoute(next);
      if (next.page === "lobby") {
        setLaunch(null);
      }
    };
    window.addEventListener("popstate", onPopState);
    return () => {
      window.removeEventListener("popstate", onPopState);
    };
  }, []);

  const go = useCallback((path: string): void => {
    navigate(path);
    setRoute(currentRoute());
  }, []);

  const toLobby = useCallback((): void => {
    setLaunch(null);
    go("/");
  }, [go]);

  const leave = useCallback((): void => {
    setLaunch(null);
    go("/");
    void leaveCasino()
      .catch(() => undefined)
      .then(() => {
        setState({ status: "anonymous" });
      });
  }, [go]);

  // Every opening of a game is a new launch (SPEC-0045 §6.1 "Tela"); one at a time.
  const launchGame = useCallback(
    async (code: string): Promise<void> => {
      if (inFlight.current !== null) return;
      inFlight.current = code;
      setOpening(code);
      setNotice(null);
      const result = await playGame(code);
      inFlight.current = null;
      setOpening(null);
      switch (result.status) {
        case "ok":
          setLaunch({ code, launchUrl: result.launchUrl });
          go(gamePath(code));
          break;
        case "unauthorized":
          leave();
          break;
        case "not-available":
          setNotice(notAvailableText);
          toLobby();
          break;
        case "unavailable":
          setNotice(unavailableText);
          toLobby();
          break;
      }
    },
    [go, leave, toLobby],
  );

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
      return route.page === "game" ? (
        <GameScreen
          key={route.code}
          code={route.code}
          launchUrl={launch?.code === route.code ? launch.launchUrl : undefined}
          onLaunch={launchGame}
          onBack={toLobby}
          onLeave={leave}
        />
      ) : (
        <LobbyScreen
          player={state.player}
          notice={notice}
          opening={opening}
          onPlay={(code) => {
            void launchGame(code);
          }}
          onLeave={leave}
        />
      );
  }
}
