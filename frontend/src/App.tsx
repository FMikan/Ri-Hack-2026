import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { AppShell } from '@/components/shared/AppShell'
import { AuthProvider } from '@/features/auth/AuthProvider'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { GuestOnly, RequireAuth } from '@/features/auth/RouteGuards'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { MatchPage } from '@/features/match/MatchPage'
import { PlayPage } from '@/features/play/PlayPage'
import { LocaleProvider } from '@/lib/i18n/LocaleProvider'

export default function App() {
  return (
    <BrowserRouter>
      <LocaleProvider>
        <AuthProvider>
          <Routes>
            <Route element={<GuestOnly />}>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />
            </Route>
            <Route element={<RequireAuth />}>
              <Route element={<AppShell />}>
                <Route path="/" element={<DashboardPage />} />
                <Route path="/play" element={<PlayPage />} />
              </Route>
              {/* The duel takes the whole screen: no rail or top bar while the clock runs. */}
              <Route path="/match/:id" element={<MatchPage />} />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AuthProvider>
      </LocaleProvider>
    </BrowserRouter>
  )
}
