// React for the render tests of SPEC-0026 (server rendering to static markup, no DOM). tests/frontend sits outside the
// pnpm workspace and has no node_modules, so React and react-dom/server are loaded through Node's resolution from the
// game-pipa app: the same React instance (same real path in the pnpm store) that game-ui renders with.
import { createRequire } from "node:module";

import type { ElementType, ReactElement } from "react";

const appRequire = createRequire(
  new URL(
    "../../../../../src/frontend/apps/game-pipa/package.json",
    import.meta.url,
  ),
);

const react = appRequire("react") as {
  createElement(
    type: ElementType,
    props?: object | null,
    ...children: unknown[]
  ): ReactElement;
};
const server = appRequire("react-dom/server") as {
  renderToStaticMarkup(element: ReactElement): string;
};

/** Static markup of `type` with `props` (effects do not run, as in the first paint). */
export function render<P extends object>(
  type: ElementType<P>,
  props: P,
): string {
  return server.renderToStaticMarkup(react.createElement(type, props));
}

/** Attributes of the element with data-testid `testId`, or null when it is not rendered. */
export function attributesOf(markup: string, testId: string): string | null {
  const match = new RegExp(
    `<[a-z]+\\b([^>]*data-testid="${testId}"[^>]*)>`,
    "u",
  ).exec(markup);
  return match?.[1] ?? null;
}

/** Text content of the element with data-testid `testId` (tags stripped), or null when it is not rendered. */
export function textOf(markup: string, testId: string): string | null {
  const open = new RegExp(
    `<([a-z]+)\\b[^>]*data-testid="${testId}"[^>]*>`,
    "u",
  ).exec(markup);
  if (open === null) return null;
  const tag = open[1] ?? "";
  const rest = markup.slice(open.index + open[0].length);
  // Nested elements of the same tag are rare in these leaves; the first closing tag ends the element.
  const end = rest.indexOf(`</${tag}>`);
  return decode((end < 0 ? rest : rest.slice(0, end)).replace(/<[^>]+>/gu, ""));
}

/** Plain text of the whole markup (tags stripped, entities decoded). */
export function plainText(markup: string): string {
  return decode(markup.replace(/<[^>]+>/gu, " "))
    .replace(/\s+/gu, " ")
    .trim();
}

function decode(text: string): string {
  return text
    .replaceAll("&quot;", '"')
    .replaceAll("&#x27;", "'")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
    .replaceAll("&nbsp;", " ")
    .replaceAll("&amp;", "&");
}
