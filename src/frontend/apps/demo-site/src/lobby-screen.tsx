import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";

import { listGames, type CasinoPlayer, type LobbyGame } from "./casino-api";
import { provider as studio } from "./game-info";

interface LobbyScreenProps {
  readonly player: CasinoPlayer;
  /** A notice of the last opening of a game (unavailable game or provider, SPEC-0045 §6.1), or null. */
  readonly notice: string | null;
  /** The code of the game being launched, or null. */
  readonly opening: string | null;
  readonly onPlay: (code: string) => void;
  readonly onLeave: () => void;
}

type CatalogState =
  | { readonly status: "loading" }
  | { readonly status: "ready"; readonly games: readonly LobbyGame[] }
  | { readonly status: "unavailable" };

type CategoryId = "all" | "crash" | "news";

interface Category {
  readonly id: CategoryId;
  readonly label: string;
}

/** Category tabs of the lobby (SPEC-0044 §6, Emenda v1): Todos = all, Crash = category crash, Novidades = isNew. */
const categories: readonly Category[] = [
  { id: "all", label: "Todos" },
  { id: "crash", label: "Crash" },
  { id: "news", label: "Novidades" },
];

/** Cards "Em breve": fixed in the front, outside the contract, not clickable (§6, Emenda v1). Shown in Todos and Crash. */
const comingSoon: readonly string[] = ["Fortune Slot", "Turbo Aviator", "Roulette Master"];

function inCategory(game: LobbyGame, category: CategoryId): boolean {
  switch (category) {
    case "all":
      return true;
    case "crash":
      return game.category === "crash";
    case "news":
      return game.isNew;
  }
}

/** Local filter of the search box (no backend search, §2). */
function matches(name: string, search: string): boolean {
  const query = search.trim().toLocaleLowerCase("pt-BR");
  return query === "" || name.toLocaleLowerCase("pt-BR").includes(query);
}

/** Index of the tab a key moves to (WAI-ARIA APG tabs: arrows wrap around, Home and End go to the ends). */
function tabIndexFor(key: string, index: number, count: number): number | undefined {
  switch (key) {
    case "ArrowRight":
      return (index + 1) % count;
    case "ArrowLeft":
      return (index - 1 + count) % count;
    case "Home":
      return 0;
    case "End":
      return count - 1;
    default:
      return undefined;
  }
}

/** The lobby of the demo casino (SPEC-0044, Stitch "Cassino Demo — Lobby"): featured game, category tabs and cards. */
export function LobbyScreen({ player, notice, opening, onPlay, onLeave }: LobbyScreenProps) {
  const ids = useId();
  const [catalog, setCatalog] = useState<CatalogState>({ status: "loading" });
  const [category, setCategory] = useState<CategoryId>("all");
  const [search, setSearch] = useState("");
  const tabs = useRef(new Map<CategoryId, HTMLButtonElement>());
  const leave = useRef(onLeave);

  useEffect(() => {
    leave.current = onLeave;
  }, [onLeave]);

  // The games come from the casino BFF (GET /casino/games), never from the Apogeu directly (§5).
  useEffect(() => {
    const controller = new AbortController();
    void listGames(controller.signal).then((result) => {
      if (controller.signal.aborted) return;
      switch (result.status) {
        case "ok":
          setCatalog({ status: "ready", games: result.games });
          break;
        case "unauthorized":
          leave.current();
          break;
        case "unavailable":
          setCatalog({ status: "unavailable" });
          break;
      }
    });
    return () => {
      controller.abort();
    };
  }, []);

  const games = catalog.status === "ready" ? catalog.games : [];
  const featured = games.find((game) => game.isNew) ?? games[0];
  const shown = games.filter((game) => inCategory(game, category) && matches(game.name, search));
  const shownSoon =
    catalog.status !== "ready" || category === "news"
      ? []
      : comingSoon.filter((name) => matches(name, search));

  const tabId = (id: CategoryId): string => `${ids}-tab-${id}`;
  const panelId = `${ids}-panel`;
  const searchId = `${ids}-search`;
  const featuredId = `${ids}-featured`;

  const onTabKey = (event: KeyboardEvent<HTMLButtonElement>, index: number): void => {
    const target = tabIndexFor(event.key, index, categories.length);
    const next = target === undefined ? undefined : categories[target];
    if (next !== undefined) {
      event.preventDefault();
      setCategory(next.id);
      tabs.current.get(next.id)?.focus();
    }
  };

  return (
    <>
      <header className="topbar">
        <p className="brand">Cassino Demo</p>
        <div className="search">
          <label htmlFor={searchId} className="visually-hidden">
            Buscar jogos
          </label>
          <input
            id={searchId}
            type="search"
            placeholder="Buscar jogos…"
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
            }}
          />
        </div>
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
      <main className="lobby" aria-busy={catalog.status === "loading"}>
        <h1 className="visually-hidden">Lobby</h1>

        {notice === null ? null : (
          <p className="notice" role="alert">
            {notice}
          </p>
        )}

        {catalog.status === "unavailable" ? (
          <p className="notice" role="alert">
            Jogos indisponíveis no momento. Tente de novo em instantes.
          </p>
        ) : null}

        {featured === undefined ? null : (
          <section className="featured" aria-labelledby={featuredId}>
            <img className="featured-art" src={featured.bannerUrl} alt="" />
            <div className="featured-content">
              {featured.isNew ? <p className="badge">Novo crash game</p> : null}
              <h2 id={featuredId}>{featured.name}</h2>
              <p className="featured-text">
                Crash game da {studio}: aposte, acompanhe o multiplicador subir e saia antes do
                crash.
              </p>
              <button
                type="button"
                className="play featured-play"
                disabled={opening !== null}
                aria-busy={opening === featured.code}
                onClick={() => {
                  onPlay(featured.code);
                }}
              >
                Jogar agora<span className="visually-hidden"> {featured.name}</span>
              </button>
            </div>
          </section>
        )}

        <div className="tabs" role="tablist" aria-label="Categorias">
          {categories.map((item, index) => {
            const selected = item.id === category;
            return (
              <button
                key={item.id}
                ref={(element) => {
                  if (element === null) tabs.current.delete(item.id);
                  else tabs.current.set(item.id, element);
                }}
                id={tabId(item.id)}
                type="button"
                role="tab"
                className="tab"
                aria-selected={selected}
                aria-controls={panelId}
                tabIndex={selected ? 0 : -1}
                onClick={() => {
                  setCategory(item.id);
                }}
                onKeyDown={(event) => {
                  onTabKey(event, index);
                }}
              >
                {item.label}
              </button>
            );
          })}
        </div>

        <div id={panelId} role="tabpanel" aria-labelledby={tabId(category)}>
          {catalog.status === "loading" ? <p className="lead">Carregando jogos…</p> : null}
          {catalog.status === "ready" && shown.length === 0 && shownSoon.length === 0 ? (
            <p className="lead">Nenhum jogo encontrado.</p>
          ) : null}
          <ul className="games">
            {shown.map((game) => (
              <li key={game.code} className="game">
                <div className="game-art">
                  <img src={game.thumbnailUrl} alt={`Arte do jogo ${game.name}`} />
                  {game.isNew ? <span className="badge game-badge">Novo</span> : null}
                </div>
                <div className="game-info">
                  <h3>{game.name}</h3>
                  <p className="studio">{studio}</p>
                  <button
                    type="button"
                    className="play"
                    disabled={opening !== null}
                    aria-busy={opening === game.code}
                    onClick={() => {
                      onPlay(game.code);
                    }}
                  >
                    {opening === game.code ? "Abrindo…" : "Jogar"}
                    <span className="visually-hidden"> {game.name}</span>
                  </button>
                </div>
              </li>
            ))}
            {shownSoon.map((name) => (
              <li key={name} className="game game-soon">
                <div className="game-art">
                  <span className="soon">Em breve</span>
                </div>
                <div className="game-info">
                  <h3>{name}</h3>
                  <p className="studio">{studio}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </main>
    </>
  );
}
