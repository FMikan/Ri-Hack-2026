declare global {
  interface ImportMetaEnv {
    // Public: bundled into the browser build. Never put secrets in VITE_* variables.
    readonly VITE_API_BASE_URL?: string
  }
}

export {}
