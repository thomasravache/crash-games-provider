// Paths of the repository and access to the front-end's npm packages (src/frontend/node_modules) from tools/contracts,
// which lives outside the pnpm workspace (SPEC-0006).
import { createRequire } from "node:module";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

export const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
export const frontendRoot = join(repositoryRoot, "src", "frontend");

const requireFromFrontend = createRequire(join(frontendRoot, "package.json"));

/** Module namespace of an npm package installed in src/frontend (`pnpm -C src/frontend install`). */
export async function importFromFrontend(name) {
  let entry;
  try {
    entry = requireFromFrontend.resolve(name);
  } catch (cause) {
    throw new Error(
      `Package '${name}' not found in src/frontend/node_modules; run \`pnpm -C src/frontend install\` first.`,
      { cause },
    );
  }
  return import(pathToFileURL(entry).href);
}

/** Formats generated TypeScript with the front-end's Prettier configuration, so `format:check` accepts it. */
export async function formatTypeScript(source) {
  const { default: prettier } = await importFromFrontend("prettier");
  const config = (await prettier.resolveConfig(join(frontendRoot, "package.json"))) ?? {};
  return prettier.format(source, { ...config, parser: "typescript" });
}
