import type { CasinoPlayer } from "./casino-api";

interface LobbyScreenProps {
  readonly player: CasinoPlayer;
  readonly onLeave: () => void;
}

/** Shell of the lobby (the games arrive with SPEC-0044): the player's nickname at the top of the page. */
export function LobbyScreen({ player, onLeave }: LobbyScreenProps) {
  return (
    <>
      <header className="topbar">
        <p className="brand">Cassino Demo</p>
        <div className="player">
          <span className="nickname">
            <span className="visually-hidden">Jogador: </span>
            {player.nickname}
          </span>
          <button type="button" className="secondary" onClick={onLeave}>
            Sair
          </button>
        </div>
      </header>
      <main className="lobby">
        <h1>Lobby</h1>
        <p className="lead">Os jogos do cassino aparecem aqui em breve.</p>
      </main>
    </>
  );
}
