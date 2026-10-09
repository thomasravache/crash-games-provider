import type { JSX, ReactNode } from "react";

import { hudText } from "./hud-text";

// State screens keep the brand as the stable main heading (h1 "Pipa", as in the HUD header); the state message is a
// subheading (h2) or plain text, never the h1.
function Screen({ children }: { readonly children: ReactNode }): JSX.Element {
  return (
    <div className="flex min-h-dvh flex-col bg-hud-canvas font-hud-body text-hud-text">
      <header className="border-b border-hud-border bg-hud-surface px-4 py-2">
        <h1 className="font-hud-display text-lg font-bold tracking-tight">{hudText.brand}</h1>
      </header>
      <main className="flex flex-1 items-center justify-center p-6 text-center">{children}</main>
    </div>
  );
}

function MessageScreen({ message }: { readonly message: string }): JSX.Element {
  return (
    <Screen>
      <h2 className="max-w-sm font-hud-display text-2xl font-bold">{message}</h2>
    </Screen>
  );
}

/** Error screen for an invalid, expired or used launch token: "Sessão expirada. Volte ao cassino para jogar." */
export function SessionExpiredScreen(): JSX.Element {
  return <MessageScreen message={hudText.sessionExpired} />;
}

/** The game could not reach the server (network or unexpected failure of the session exchange). */
export function ConnectionFailedScreen(): JSX.Element {
  return <MessageScreen message={hudText.connectionFailed} />;
}

/** While the launch token is exchanged and the sala joined. */
export function LoadingScreen(): JSX.Element {
  return (
    <Screen>
      <p role="status" className="text-hud-muted">
        {hudText.loading}
      </p>
    </Screen>
  );
}
