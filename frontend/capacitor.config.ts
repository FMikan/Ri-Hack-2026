import type { CapacitorConfig } from '@capacitor/cli'

const config: CapacitorConfig = {
  appId: 'tech.stackoff.app',
  appName: 'StackOff',
  webDir: 'dist',
  // fetch goes through native HTTP, so the WebView origin (https://localhost) never hits CORS on stackoff.tech.
  // The bundle is built with VITE_API_BASE_URL=https://stackoff.tech (build:android).
  plugins: { CapacitorHttp: { enabled: true } },
}

export default config
