import type { JSX } from "react";

import { hudText } from "./hud-text";

function Screen({ message }: { readonly message: string }): JSX.Element {
  return (
    <main className="flex min-h-dvh items-center justify-center bg-hud-canvas p-6 text-center font-hud-body text-hud-text">
      <h1 className="max-w-sm font-hud-display text-2xl font-bold">{message}</h1>
    </main>
  );
}

/** Error screen for an invalid, expired or used launch token: "Sessão expirada. Volte ao cassino para jogar." */
export function SessionExpiredScreen(): JSX.Element {
  return <Screen message={hudText.sessionExpired} />;
}

/** The game could not reach the server (network or unexpected failure of the session exchange). */
export function ConnectionFailedScreen(): JSX.Element {
  return <Screen message={hudText.connectionFailed} />;
}

/** While the launch token is exchanged and the sala joined. */
export function LoadingScreen(): JSX.Element {
  return (
    <main className="flex min-h-dvh items-center justify-center bg-hud-canvas font-hud-body text-hud-muted">
      <h1 className="sr-only">Pipa</h1>
      <p role="status">{hudText.loading}</p>
    </main>
  );
}
