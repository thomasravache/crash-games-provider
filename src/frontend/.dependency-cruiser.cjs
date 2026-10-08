// dependency-cruiser rules of ADR-0004 (one rule per verifiable item). Paths are relative to the directory the cruise
// runs from: src/frontend for `pnpm test:arch`, or a fixture tree with the same layout (tests/frontend/arch-fixtures).
/** @type {import('dependency-cruiser').IConfiguration} */
module.exports = {
  forbidden: [
    {
      name: "contracts-depend-on-nothing",
      comment:
        "ADR-0004: packages/contracts is generated from the backend (ADR-0008) and is the start of the flow " +
        "contracts → game-sdk → game-ui/scene-kit → apps; it imports no other workspace code.",
      severity: "error",
      from: { path: "^packages/contracts/" },
      to: { path: "^(apps|packages)/", pathNot: "^packages/contracts/" },
    },
    {
      name: "game-sdk-depends-only-on-contracts",
      comment:
        "ADR-0004: game-sdk (network + round state) only depends on contracts; never on game-ui, scene-kit or apps.",
      severity: "error",
      from: { path: "^packages/game-sdk/" },
      to: { path: "^(apps/|packages/(game-ui|scene-kit)/)" },
    },
    {
      name: "packages-do-not-depend-on-apps",
      comment: "ADR-0004: shared packages never import an app; the flow ends in apps.",
      severity: "error",
      from: { path: "^packages/" },
      to: { path: "^apps/" },
    },
    {
      name: "scene-does-not-import-transport",
      comment:
        "ADR-0004: scene-kit and the scene of the apps do not import the network layer nor the transport types; " +
        "they only read the state exposed by game-sdk. Checked by reachability, so the transport is also rejected " +
        "when it arrives through the game-sdk entry (a re-export) or any other module in between.",
      severity: "error",
      from: { path: ["^packages/scene-kit/", "^apps/[^/]+/src/scene/"] },
      to: { path: "^packages/game-sdk/src/transport/", reachable: true },
    },
    {
      name: "no-app-imports-another-app",
      comment: "ADR-0004: no app imports another app; shared code goes to packages/.",
      severity: "error",
      from: { path: "^apps/([^/]+)/" },
      to: { path: "^apps/", pathNot: "^apps/$1/" },
    },
    {
      name: "no-import-outside-the-workspace",
      comment:
        "ADR-0004: apps and packages only import workspace code and declared npm packages, never files outside " +
        "src/frontend (for example the backend or the tests).",
      severity: "error",
      from: { path: "^(apps|packages)/" },
      to: { path: "^\\.\\./" },
    },
    {
      name: "no-circular",
      comment: "ADR-0004: the flow has one direction, so no module takes part in a cycle.",
      severity: "error",
      from: {},
      to: { circular: true },
    },
    {
      name: "not-to-unresolvable",
      comment: "An import that does not resolve hides a broken or undeclared dependency.",
      severity: "error",
      from: {},
      to: { couldNotResolve: true },
    },
  ],
  options: {
    doNotFollow: { path: "node_modules" },
    exclude: { path: "(^|/)(node_modules|dist|coverage)/" },
    // Type-only imports count: transport types are forbidden in the scene as much as transport code.
    tsPreCompilationDeps: true,
    enhancedResolveOptions: {
      exportsFields: ["exports"],
      conditionNames: ["import", "types", "default"],
      extensions: [".ts", ".tsx", ".js", ".mjs", ".cjs", ".json"],
    },
  },
};
