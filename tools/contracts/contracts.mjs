// Contract generation and drift check (ADR-0008, SPEC-0006). Run after `dotnet build Apogeu.slnx`:
//
//   node tools/contracts/contracts.mjs generate   → artifacts/openapi/v1.json + src/frontend/packages/contracts/src/**
//   node tools/contracts/contracts.mjs check      → exit 0 when the committed contracts match a fresh generation,
//                                                   exit 1 with a unified diff otherwise
import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, statSync } from "node:fs";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { repositoryRoot, formatTypeScript } from "./frontend-modules.mjs";
import { hubsToTypeScript } from "./hubs-to-ts.mjs";
import { openApiToTypeScript } from "./openapi-to-ts.mjs";
import { scalarDeclarations } from "./scalars.mjs";

/** Build outputs the contracts come from (`dotnet build Apogeu.slnx`). */
const buildOpenApi = join(repositoryRoot, "src", "backend", "Host", "obj", "openapi", "v1.json");
const hubExporterDirectory = join(repositoryRoot, "tools", "contracts", "HubContracts", "bin");
const hubExporterAssembly = "Apogeu.HubContracts.dll";

/** Generated areas, relative to the repository root; each is entirely owned by the generator. */
const openApiArea = join("artifacts", "openapi");
const contractsArea = join("src", "frontend", "packages", "contracts", "src");

const header = "// Generated from the backend (ADR-0008, SPEC-0006). Never edit by hand.\n";

const files = {
  [join(contractsArea, "index.ts")]: () =>
    formatTypeScript(
      `${header}\nexport * from "./scalars";\nexport * from "./rest";\nexport * from "./hubs";\n`,
    ),
  [join(contractsArea, "scalars.ts")]: () => formatTypeScript(`${header}\n${scalarDeclarations}\n`),
  [join(contractsArea, "rest", "index.ts")]: () =>
    formatTypeScript(`${header}\nexport type * from "./schema";\nexport * from "./client";\n`),
  [join(contractsArea, "rest", "client.ts")]: () =>
    formatTypeScript(
      `${header}
import createClient, { type Client, type ClientOptions } from "openapi-fetch";
import type { paths } from "./schema";

/** Typed client of the REST API (openapi-fetch): paths, parameters and bodies come from the OpenAPI document. */
export type RestClient = Client<paths>;

export function createRestClient(options?: ClientOptions): RestClient {
  return createClient<paths>(options);
}
`,
    ),
};

async function generate(root) {
  if (!existsSync(buildOpenApi)) {
    throw new Error(`Missing ${buildOpenApi}; run \`dotnet build Apogeu.slnx\` first.`);
  }
  const document = JSON.parse(await readFile(buildOpenApi, "utf8"));
  const manifest = await exportHubManifest();

  await rm(join(root, openApiArea), { recursive: true, force: true });
  await rm(join(root, contractsArea), { recursive: true, force: true });

  const outputs = {
    [join(openApiArea, "v1.json")]: async () => `${JSON.stringify(document, null, 2)}\n`,
    [join(contractsArea, "rest", "schema.ts")]: () =>
      openApiToTypeScript(document, { scalarsFrom: "../scalars" }),
    [join(contractsArea, "hubs", "index.ts")]: () =>
      hubsToTypeScript(manifest, { scalarsFrom: "../scalars" }),
    ...files,
  };
  for (const [path, render] of Object.entries(outputs)) {
    const target = join(root, path);
    await mkdir(dirname(target), { recursive: true });
    await writeFile(target, await render());
  }
}

/** Runs the hub exporter built with the solution (the most recent configuration) and returns its manifest. */
async function exportHubManifest() {
  const candidates = ["Debug", "Release"]
    .map((configuration) => join(hubExporterDirectory, configuration, "net10.0", hubExporterAssembly))
    .filter((path) => existsSync(path))
    .sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs);
  if (candidates.length === 0) {
    throw new Error(`Missing ${hubExporterAssembly}; run \`dotnet build Apogeu.slnx\` first.`);
  }

  const scratch = await mkdtemp(join(tmpdir(), "apogeu-hubs-"));
  try {
    const manifest = join(scratch, "hubs.json");
    execFileSync("dotnet", [candidates[0], "--output", manifest], { stdio: ["ignore", "inherit", "inherit"] });
    return JSON.parse(await readFile(manifest, "utf8"));
  } finally {
    await rm(scratch, { recursive: true, force: true });
  }
}

async function check() {
  const scratch = await mkdtemp(join(tmpdir(), "apogeu-contracts-"));
  try {
    await generate(scratch);
    let drift = false;
    for (const area of [openApiArea, contractsArea]) {
      const committed = join(repositoryRoot, area);
      if (!existsSync(committed)) {
        process.stdout.write(`Missing generated contracts: ${area}\n`);
        drift = true;
        continue;
      }
      const diff = spawnSync(
        "git",
        ["diff", "--no-index", "--no-color", "--", committed, join(scratch, area)],
        { encoding: "utf8" },
      );
      if (diff.status !== 0) {
        process.stdout.write(diff.stdout);
        process.stderr.write(diff.stderr);
        drift = true;
      }
    }
    if (drift) {
      process.stderr.write(
        "\nThe committed contracts differ from the backend. Run `pnpm -C src/frontend contracts:generate` and commit the result.\n",
      );
      process.exitCode = 1;
    }
  } finally {
    await rm(scratch, { recursive: true, force: true });
  }
}

const command = process.argv[2];
if (command === "generate") {
  await generate(repositoryRoot);
} else if (command === "check") {
  await check();
} else {
  process.stderr.write("Usage: node tools/contracts/contracts.mjs <generate|check>\n");
  process.exitCode = 2;
}
