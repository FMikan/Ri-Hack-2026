import { Navigate, Outlet, useLocation } from 'react-router'
import { Button } from '@/components/ui/button'
import { useLocale } from '@/lib/i18n/locale-context'
import { useAuth } from './auth-context'

// Pages that need a session. Anonymous visitors go to /login, which returns them here after logging in.
export function RequireAuth() {
  const { session } = useAuth()
  const location = useLocation()

  if (session.status === 'anonymous')
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  if (session.status !== 'authenticated') return <SessionPending />
  return <Outlet />
}

// Login and register. A signed-in user goes back to where they came from (state set by RequireAuth, never the URL).
export function GuestOnly() {
  const { session } = useAuth()
  const location = useLocation()

  if (session.status === 'authenticated') {
    const from = (location.state as { from?: string } | null)?.from
    return <Navigate to={from ?? '/'} replace />
  }
  if (session.status !== 'anonymous') return <SessionPending />
  return <Outlet />
}

function SessionPending() {
  const { session, retry } = useAuth()
  const { t } = useLocale()

  return (
    <main className="grid min-h-svh place-items-center px-4">
      {session.status === 'error' ? (
        <div role="alert" className="grid max-w-sm justify-items-center gap-4 text-center">
          <p className="text-pretty">{t.common.serverUnreachable}</p>
          <Button variant="outline" onClick={retry}>
            {t.common.retry}
          </Button>
        </div>
      ) : (
        <p role="status" className="font-mono text-sm text-muted-foreground">
          {t.common.loading}
        </p>
      )}
    </main>
  )
}
