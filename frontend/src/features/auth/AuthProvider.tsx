import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, refreshSession, setUnauthenticatedHandler } from '@/lib/api-client'
import type { MeDto } from '@/types/api'
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

  // Same data, same session object: the top bar and every avatar read it, so a poll must not re-render them for nothing.
  const setUser = useCallback(
    (user: MeDto) =>
      setSession((previous) =>
        previous.status === 'authenticated' && JSON.stringify(previous.user) === JSON.stringify(user)
          ? previous
          : { status: 'authenticated', user },
      ),
    [],
  )
  const refreshUser = useCallback(async () => setUser(await api.me()), [setUser])

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
      updateMe: async (body) => setUser(await api.updateMe(body)),
      setUser,
      refreshUser,
    }),
    [session, restore, setUser, refreshUser],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
