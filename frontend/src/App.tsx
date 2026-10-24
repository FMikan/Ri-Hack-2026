import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { AppShell } from '@/components/shared/AppShell'
import { AuthProvider } from '@/features/auth/AuthProvider'
import { ForgotPasswordPage } from '@/features/auth/ForgotPasswordPage'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { ResetPasswordPage } from '@/features/auth/ResetPasswordPage'
import { GuestOnly, RequireAuth } from '@/features/auth/RouteGuards'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { LeaderboardsPage } from '@/features/leaderboards/LeaderboardsPage'
import { LessonPage } from '@/features/lesson/LessonPage'
import { MatchPage } from '@/features/match/MatchPage'
import { PracticePage } from '@/features/practice/PracticePage'
import { ProfilePage } from '@/features/profile/ProfilePage'
import { ReviewPage } from '@/features/review/ReviewPage'
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
              <Route path="/forgot-password" element={<ForgotPasswordPage />} />
            </Route>
            {/* Public: the e-mail link works whether or not this browser is logged in. */}
            <Route path="/reset-password" element={<ResetPasswordPage />} />
            <Route element={<RequireAuth />}>
              <Route element={<AppShell />}>
                <Route path="/" element={<DashboardPage />} />
                <Route path="/practice" element={<PracticePage />} />
                <Route path="/leaderboards" element={<LeaderboardsPage />} />
                <Route path="/profile" element={<ProfilePage />} />
                <Route path="/match/:id/review" element={<ReviewPage />} />
                <Route path="/lesson/:id" element={<LessonPage />} />
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
