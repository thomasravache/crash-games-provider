/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base URL of the API (Host), injected by the Aspire AppHost (SPEC-0010). */
  readonly VITE_APOGEU_API_URL?: string;
  /** "true" builds the game with the test hook (SPEC-0010 §6). */
  readonly VITE_APOGEU_TEST_HOOK?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
