// The "Som" control's props from the SoundController (SPEC-0018 §6); undefined while the sound is not ready.

import type { SoundToggleProps } from "@apogeu/game-ui";
import { useSyncExternalStore } from "react";

import type { SoundController } from "./sound-controller";

const noSound = {
  subscribe: () => () => undefined,
  getSnapshot: () => null,
} as const;

export function useSound(controller: SoundController | undefined): SoundToggleProps | undefined {
  const store = controller ?? noSound;
  const preference = useSyncExternalStore(store.subscribe, store.getSnapshot);
  if (controller === undefined || preference === null) return undefined;
  return {
    muted: preference.muted,
    volume: preference.volume,
    onMutedChange: (muted) => {
      controller.setMuted(muted);
    },
    onVolumeChange: (volume) => {
      controller.setVolume(volume);
    },
  };
}
