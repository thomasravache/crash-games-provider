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

/** Upper bound of Prettier passes: the output of a well-behaved input stabilises after two or three. */
const maxFormatPasses = 10;

/**
 * Formats generated TypeScript with the front-end's Prettier configuration, so `format:check` accepts it. One pass is not
 * idempotent for some long signatures (a second pass reflows them again), so Prettier runs until its output stops changing.
 */
export async function formatTypeScript(source) {
  const { default: prettier } = await importFromFrontend("prettier");
  const config = (await prettier.resolveConfig(join(frontendRoot, "package.json"))) ?? {};
  let current = source;
  for (let pass = 0; pass < maxFormatPasses; pass++) {
    const formatted = await prettier.format(current, { ...config, parser: "typescript" });
    if (formatted === current) {
      return formatted;
    }
    current = formatted;
  }
  throw new Error(`Prettier did not reach a stable output after ${maxFormatPasses} passes.`);
}
