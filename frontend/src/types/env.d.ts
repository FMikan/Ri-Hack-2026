declare global {
  interface ImportMetaEnv {
    // Public: bundled into the browser build. Never put secrets in VITE_* variables.
    readonly VITE_API_BASE_URL?: string
    // 'true' fills dashboard stats with test numbers (src/lib/fixtures/demo.ts).
    readonly VITE_DEMO_DATA?: string
  }
}

export {}
