import { newAudioRig } from "./support/audio";
import { describeGameAudioContract } from "./support/game-audio-contract";

// SPEC-0018 CT-01: PipaAudio passes the GameAudio contract suite, driven by the state sequences of SPEC-0010@1 (the same
// ones as the GameAnimator contract suite), on the OfflineAudioContext double (amendment IMP-01).

describeGameAudioContract("SPEC-0018:CT-01", () => {
  const rig = newAudioRig();
  return {
    audio: rig.audio,
    advance: (ms) => {
      rig.advance(ms);
    },
    contextsCreated: () => rig.contexts.length,
  };
});
