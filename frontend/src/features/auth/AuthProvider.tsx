import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, refreshSession, setUnauthenticatedHandler } from '@/lib/api-client'
import { AuthContext, type AuthState, type Session } from './auth-context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session>({ status: 'loading' })

  // The access token is memory-only, so every page load restores the session from the stored refresh token.
  const restore = useCallback(() => {
    refreshSession().then(
      (auth) => setSession(auth ? { status: 'authenticated', user: auth.user } : { status: 'anonymous' }),
      () => setSession({ status: 'error' }),
    )
  }, [])

  useEffect(() => {
    setUnauthenticatedHandler(() => setSession({ status: 'anonymous' }))
    restore()
  }, [restore])

  const value = useMemo<AuthState>(
    () => ({
      session,
      retry: () => {
        setSession({ status: 'loading' })
        restore()
      },
      login: async (body) => {
        const auth = await api.login(body)
        setSession({ status: 'authenticated', user: auth.user })
      },
      register: async (body) => {
        const auth = await api.register(body)
        setSession({ status: 'authenticated', user: auth.user })
      },
      logout: async () => {
        await api.logout()
        setSession({ status: 'anonymous' })
      },
      updateMe: async (body) => {
        const user = await api.updateMe(body)
        setSession({ status: 'authenticated', user })
      },
    }),
    [session, restore],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
