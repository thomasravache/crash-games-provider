import { SessionError, type GameClient, type SessionInfo } from "@apogeu/game-sdk";
import type { RoundStateSource } from "@apogeu/game-sdk/state";
import {
  ConnectionFailedScreen,
  FairnessDialog,
  Hud,
  LoadingScreen,
  SessionExpiredScreen,
  useRoundState,
} from "@apogeu/game-ui";
import { useEffect, useRef, useState, type JSX } from "react";

import { demoBalance } from "./config";

type Screen =
  | { readonly kind: "loading" }
  | { readonly kind: "game"; readonly session: SessionInfo }
  | { readonly kind: "expired" }
  | { readonly kind: "failed" };

export interface AppProps {
  readonly client: GameClient;
  /** The session start, begun once before React renders (the launch token is single use). */
  readonly session: Promise<SessionInfo>;
}

/** Host of the PixiJS scene, loaded on demand (the renderer is not part of the initial bundle). */
function Scene({ source }: { readonly source: RoundStateSource }): JSX.Element {
  const host = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const element = host.current;
    if (element === null) return undefined;
    let unmount: (() => void) | undefined;
    let cancelled = false;
    void import("./scene/mount-scene")
      .then(({ mountPipaScene }) => mountPipaScene(element, source))
      .then((stop) => {
        if (cancelled) stop();
        else unmount = stop;
      })
      // Without WebGL/WebGPU the game still works: the HUD carries every state (ADR-0020).
      .catch(() => undefined);
    return () => {
      cancelled = true;
      unmount?.();
    };
  }, [source]);
  return <div ref={host} className="h-full w-full" />;
}

function Game({
  client,
  session,
}: {
  readonly client: GameClient;
  readonly session: SessionInfo;
}): JSX.Element {
  const state = useRoundState(client.store);
  const [fairnessOpen, setFairnessOpen] = useState(false);

  return (
    <Hud
      state={state}
      nickname={session.nickname}
      demoBalance={demoBalance}
      onOpenFairness={() => {
        setFairnessOpen(true);
      }}
      scene={<Scene source={client.store} />}
    >
      {fairnessOpen ? (
        <FairnessDialog
          history={state.history}
          loadVerification={(roundId) => client.loadVerification(roundId)}
          onClose={() => {
            setFairnessOpen(false);
          }}
        />
      ) : null}
    </Hud>
  );
}

export function App({ client, session }: AppProps): JSX.Element {
  const [screen, setScreen] = useState<Screen>({ kind: "loading" });

  useEffect(() => {
    let active = true;
    session.then(
      (info) => {
        if (active) setScreen({ kind: "game", session: info });
      },
      (error: unknown) => {
        if (!active) return;
        const rejected = error instanceof SessionError && error.isLaunchTokenRejected;
        setScreen({ kind: rejected ? "expired" : "failed" });
      },
    );
    return () => {
      active = false;
    };
  }, [session]);

  switch (screen.kind) {
    case "loading":
      return <LoadingScreen />;
    case "expired":
      return <SessionExpiredScreen />;
    case "failed":
      return <ConnectionFailedScreen />;
    case "game":
      return <Game client={client} session={screen.session} />;
  }
}
