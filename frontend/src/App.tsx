import { SystemStatus } from '@/features/status/SystemStatus'
import { API_BASE_URL } from '@/lib/api-client'

export default function App() {
  return (
    <div className="mx-auto grid min-h-svh max-w-5xl grid-rows-[auto_1fr_auto] gap-10 px-4 py-10 sm:px-6 sm:py-16">
      <header className="grid max-w-2xl gap-3">
        <h1 className="font-heading text-3xl font-semibold tracking-tight sm:text-4xl">Ri-Hack 2026</h1>
        <p className="text-muted-foreground">
          Starter for a React, ASP.NET Core and PostgreSQL app. This page checks that every layer is wired
          together. Replace it with the real product.
        </p>
      </header>

      <main className="grid items-start gap-6 md:grid-cols-2">
        <SystemStatus />
      </main>

      <footer className="flex flex-wrap gap-x-6 gap-y-2 font-mono text-xs text-muted-foreground">
        <span>API {API_BASE_URL || 'same origin'}</span>
        {import.meta.env.DEV && (
          <a
            href={`${API_BASE_URL}/scalar`}
            className="underline underline-offset-4 hover:text-foreground focus-visible:rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            API reference (Scalar)
          </a>
        )}
      </footer>
    </div>
  )
}
