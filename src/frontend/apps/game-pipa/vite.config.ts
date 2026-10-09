import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, type Plugin } from "vite";

/** ADR-0019 / SPEC-0010 §7.6: initial bundle of the game ≤ 1.5 MB gzip (entry chunk, its static imports and CSS). */
const initialBundleBudgetBytes = 1.5 * 1024 * 1024;

async function gzipSize(content: string | Uint8Array): Promise<number> {
  const bytes = typeof content === "string" ? new TextEncoder().encode(content) : content;
  const stream = new Blob([new Uint8Array(bytes)])
    .stream()
    .pipeThrough(new CompressionStream("gzip"));
  return (await new Response(stream).arrayBuffer()).byteLength;
}

function bundleBudget(limitBytes: number): Plugin {
  return {
    name: "apogeu-bundle-budget",
    apply: "build",
    async generateBundle(_options, bundle) {
      const initial = new Set<string>();
      const visit = (fileName: string): void => {
        const output = bundle[fileName];
        if (output === undefined || initial.has(fileName)) return;
        initial.add(fileName);
        if (output.type === "chunk") {
          output.imports.forEach(visit);
          output.viteMetadata?.importedCss.forEach(visit);
        }
      };
      for (const output of Object.values(bundle)) {
        if (output.type === "chunk" && output.isEntry) visit(output.fileName);
      }
      let total = 0;
      for (const fileName of initial) {
        const output = bundle[fileName];
        if (output === undefined) continue;
        total += await gzipSize(output.type === "chunk" ? output.code : output.source);
      }
      const summary = `initial bundle ${(total / 1024).toFixed(1)} KiB gzip (budget ${(limitBytes / 1024).toFixed(0)} KiB)`;
      if (total > limitBytes) this.error(`Bundle budget exceeded: ${summary}`);
      this.info(summary);
    },
  };
}

export default defineConfig({
  plugins: [react(), tailwindcss(), bundleBudget(initialBundleBudgetBytes)],
});
