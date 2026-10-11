import { createGameClient, SessionError, takeLaunchToken, type RoundStore } from "@apogeu/game-sdk";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import { App } from "./App";
import { createSoundController } from "./audio/sound-controller";
import { apiBaseUrl } from "./config";
import "./styles.css";
import { installTestHook } from "./test-hook";

const container = document.getElementById("root");
if (container === null) {
  throw new Error("Root element #root not found.");
}

/** Interpolates the multiplier every frame (ADR-0019); the store ignores ticks outside the flight. */
function runFrameTicker(store: RoundStore): void {
  const frame = (): void => {
    store.tick(Date.now());
    requestAnimationFrame(frame);
  };
  requestAnimationFrame(frame);
}

// The launch token leaves the address bar before anything else (SPEC-0010 §5) and is exchanged exactly once, outside
// React (StrictMode runs effects twice; the token is single use).
const launchToken = takeLaunchToken(window.location, window.history);
const client = createGameClient({ apiBaseUrl: apiBaseUrl() });
installTestHook(client.store);
runFrameTicker(client.store);
// The sound (SPEC-0018) loads on demand and stays silent until the player's first gesture.
const sound = createSoundController(client.store);
const session =
  launchToken === null
    ? Promise.reject(new SessionError("LAUNCH_TOKEN_INVALID", "No launch token in the URL."))
    : client.start(launchToken);
// Handled by App; this keeps an early rejection from being reported as unhandled before React subscribes.
session.catch(() => undefined);

createRoot(container).render(
  <StrictMode>
    <App client={client} session={session} sound={sound} />
  </StrictMode>,
);
