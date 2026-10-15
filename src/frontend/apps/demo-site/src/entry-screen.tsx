import { useId, useState, type SubmitEvent } from "react";

import {
  enterCasino,
  nicknamePattern,
  supportedCurrency,
  type CasinoPlayer,
  type EntryErrorCode,
} from "./casino-api";

const errorText: Record<EntryErrorCode, string> = {
  INVALID_NICKNAME: "Apelido inválido: use de 3 a 20 letras, números ou _.",
  CURRENCY_NOT_SUPPORTED: "Moeda não suportada: o cassino demo aceita apenas BRL.",
  UNAVAILABLE: "Não foi possível entrar agora. Tente de novo em instantes.",
};

interface EntryScreenProps {
  readonly onEntered: (player: CasinoPlayer) => void;
}

/** Entry screen of the demo casino (SPEC-0043, Stitch "Cassino Demo — Entrada"). */
export function EntryScreen({ onEntered }: EntryScreenProps) {
  const nicknameId = useId();
  const hintId = useId();
  const errorId = useId();
  const currencyId = useId();
  const [nickname, setNickname] = useState("");
  const [currency, setCurrency] = useState(supportedCurrency);
  const [error, setError] = useState<EntryErrorCode | null>(null);
  const [pending, setPending] = useState(false);

  const submit = async (event: SubmitEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    if (pending) return;
    const trimmed = nickname.trim();
    if (!nicknamePattern.test(trimmed)) {
      setError("INVALID_NICKNAME");
      return;
    }
    setPending(true);
    setError(null);
    const result = await enterCasino(trimmed, currency);
    setPending(false);
    if (result.ok) {
      onEntered(result.player);
    } else {
      setError(result.code);
    }
  };

  const describedBy = error === null ? hintId : `${hintId} ${errorId}`;

  return (
    <>
      <header className="topbar">
        <p className="brand">Cassino Demo</p>
      </header>
      <main className="entry">
        <section className="card" aria-labelledby={`${nicknameId}-title`}>
          <h1 id={`${nicknameId}-title`}>Entrar no cassino</h1>
          <p className="lead">
            Escolha um apelido para jogar com saldo fictício. Nenhum dado pessoal é pedido.
          </p>
          <form
            noValidate
            onSubmit={(event) => {
              void submit(event);
            }}
          >
            <div className="field">
              <label htmlFor={nicknameId}>Apelido</label>
              <input
                id={nicknameId}
                name="nickname"
                type="text"
                autoComplete="nickname"
                maxLength={20}
                required
                value={nickname}
                aria-describedby={describedBy}
                aria-invalid={error === "INVALID_NICKNAME"}
                onChange={(event) => {
                  setNickname(event.target.value);
                }}
              />
              <p id={hintId} className="hint">
                De 3 a 20 caracteres: letras, números ou _.
              </p>
            </div>
            <div className="field">
              <label htmlFor={currencyId}>Moeda</label>
              <select
                id={currencyId}
                name="currency"
                value={currency}
                onChange={(event) => {
                  setCurrency(event.target.value);
                }}
              >
                <option value={supportedCurrency}>BRL — Real brasileiro</option>
              </select>
            </div>
            {error === null ? null : (
              <p id={errorId} className="error" role="alert">
                {errorText[error]}
              </p>
            )}
            <button type="submit" className="primary" disabled={pending}>
              {pending ? "Entrando…" : "Entrar"}
            </button>
          </form>
        </section>
      </main>
    </>
  );
}
