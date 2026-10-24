import { createContext, useContext } from 'react'
import type { LoginRequest, MeDto, RegisterRequest, UpdateMeRequest } from '@/types/api'

export type Session =
  | { status: 'loading' }
  | { status: 'error' } // the API was unreachable while restoring the session
  | { status: 'anonymous' }
  | { status: 'authenticated'; user: MeDto }

export interface AuthState {
  session: Session
  retry: () => void
  login: (body: LoginRequest, remember: boolean) => Promise<void>
  register: (body: RegisterRequest) => Promise<void>
  logout: () => Promise<void>
  updateMe: (body: UpdateMeRequest) => Promise<void>
  setUser: (user: MeDto) => void // a fresh MeDto from a purchase, the equipment or the dashboard
  refreshUser: () => Promise<void> // after something changed coins, XP or rating on the server
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth() {
  const auth = useContext(AuthContext)
  if (!auth) throw new Error('useAuth must be used inside <AuthProvider>.')
  return auth
}
