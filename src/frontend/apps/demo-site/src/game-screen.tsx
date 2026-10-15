import { useEffect, useId, useRef, useState, type MouseEvent } from "react";

import { aboutGame, provider } from "./game-info";

interface GameScreenProps {
  readonly code: string;
  /** The single-use launch URL of the Apogeu, or undefined while the game is being launched. */
  readonly launchUrl: string | undefined;
  readonly onLaunch: (code: string) => Promise<void>;
  readonly onBack: () => void;
}

/** The minimum the game needs in the iframe (SPEC-0045 §6): its scripts, its own origin (API and hub) and popups. */
const gameSandbox = "allow-scripts allow-same-origin allow-popups";

/**
 * The game page of the demo casino (SPEC-0045 §6, Stitch "Cassino Demo — Pipa (Refinado)"): the frame of the casino
 * (back to the lobby, the game name, fullscreen), the game of the Apogeu in an iframe and the "Sobre o jogo" column.
 */
export function GameScreen({ code, launchUrl, onLaunch, onBack }: GameScreenProps) {
  const ids = useId();
  const stage = useRef<HTMLDivElement>(null);
  const [fullscreen, setFullscreen] = useState(false);
  const about = aboutGame(code);
  const name = about?.name ?? code;
  const titleId = `${ids}-title`;
  const aboutId = `${ids}-about`;

  // Opened without a launch (a reload or the browser history): launch it again, the previous URL is single use.
  useEffect(() => {
    if (launchUrl === undefined) {
      void onLaunch(code);
    }
  }, [code, launchUrl, onLaunch]);

  useEffect(() => {
    const onChange = (): void => {
      setFullscreen(document.fullscreenElement !== null);
    };
    document.addEventListener("fullscreenchange", onChange);
    return () => {
      document.removeEventListener("fullscreenchange", onChange);
    };
  }, []);

  const toggleFullscreen = (): void => {
    if (document.fullscreenElement !== null) {
      void document.exitFullscreen().catch(() => undefined);
    } else {
      void stage.current?.requestFullscreen().catch(() => undefined);
    }
  };

  const back = (event: MouseEvent<HTMLAnchorElement>): void => {
    event.preventDefault();
    onBack();
  };

  return (
    <>
      {/* Labelled landmarks: the game in the iframe has its own banner, so the casino frame names its own. */}
      <header className="topbar game-topbar" aria-label="Cassino Demo">
        <a href="/" className="back" onClick={back}>
          <span aria-hidden="true">←</span> Voltar ao lobby
        </a>
        <h1 id={titleId} className="game-title">
          {name}
        </h1>
        <button type="button" className="secondary" onClick={toggleFullscreen}>
          {fullscreen ? "Sair da tela cheia" : "Tela cheia"}
        </button>
      </header>
      <div className="game-layout">
        <main className="game-main" aria-labelledby={titleId} aria-busy={launchUrl === undefined}>
          <div ref={stage} className="game-stage">
            {launchUrl === undefined ? (
              <p className="lead game-loading">Abrindo o jogo…</p>
            ) : (
              <iframe
                className="game-frame"
                src={launchUrl}
                title={`Jogo ${name}`}
                sandbox={gameSandbox}
                allow="fullscreen"
              />
            )}
          </div>
        </main>
        <aside className="about" aria-labelledby={aboutId}>
          <h2 id={aboutId}>Sobre o jogo</h2>
          <dl className="facts">
            {about === undefined ? null : (
              <>
                <div className="fact">
                  <dt>RTP</dt>
                  <dd>{about.rtp}</dd>
                </div>
                <div className="fact">
                  <dt>Ganho máximo</dt>
                  <dd>{about.maxWin}</dd>
                </div>
                {about.provablyFair ? (
                  <div className="fact">
                    <dt>Segurança</dt>
                    <dd>
                      <span className="fair">Comprovadamente justo</span>
                    </dd>
                  </div>
                ) : null}
              </>
            )}
            <div className="fact">
              <dt>Provedor</dt>
              <dd>{provider}</dd>
            </div>
          </dl>
          <div className="demo-note">
            <p className="demo-note-title">Modo demonstração</p>
            <p>
              Você está jogando com créditos fictícios para testes. Nenhuma transação financeira
              real está ocorrendo.
            </p>
          </div>
        </aside>
      </div>
      <footer className="game-footer" aria-label="Rodapé do Cassino Demo">
        <p>Ambiente de demonstração — sem dinheiro real.</p>
        <p>
          Jogos por <strong>{provider}</strong>
        </p>
      </footer>
    </>
  );
}
