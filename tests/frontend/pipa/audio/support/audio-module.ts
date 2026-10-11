// The sound engine of SPEC-0018 (packages/scene-kit/src/audio). Reached by its source path: the `touches` of SPEC-0018
// cover only the audio folder, so the scene-kit entry (src/index.ts) may not re-export it yet. Every test imports the
// engine from here, so moving to `@apogeu/scene-kit` is a one-line change.
export * from "../../../../../src/frontend/packages/scene-kit/src/audio/index";
