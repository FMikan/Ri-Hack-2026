import { Link, useNavigate, useSearchParams } from 'react-router'
import { api } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import { useAuth } from './auth-context'
import { AuthForm } from './AuthForm'
import { AuthLayout } from './AuthLayout'
import { TextField } from './TextField'
import { useAuthForm } from './use-auth-form'
import { validateNewPassword } from './validation'

// Opened from the e-mail link (/reset-password?token=...). A new password logs out every session, so it ends on login.
export function ResetPasswordPage() {
  const { t } = useLocale()
  const copy = t.auth.reset
  const [params] = useSearchParams()
  const token = params.get('token')
  const navigate = useNavigate()
  const { session, logout } = useAuth()
  const form = useAuthForm(['password'], (values) => validateNewPassword(values, t.auth.errors), async ({ password }) => {
    await api.resetPassword({ token: token ?? '', newPassword: password })
    if (session.status === 'authenticated') await logout()
    navigate('/login', { replace: true, state: { notice: 'password-reset' } })
  })

  if (!token)
    return (
      <AuthLayout title={copy.documentTitle}>
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-balance sm:text-3xl">{copy.title}</h1>
        <p className="mt-2 text-pretty text-muted-foreground">{copy.noToken}</p>
        <Link to="/forgot-password" className="mt-6 inline-block rounded-sm font-medium underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
          {copy.switchLink}
        </Link>
      </AuthLayout>
    )

  return (
    <AuthForm copy={copy} switchTo="/forgot-password" {...form}>
      <TextField
        name="password"
        type="password"
        label={copy.field}
        hint={t.auth.hints.password}
        autoComplete="new-password"
        error={form.errors.password}
      />
    </AuthForm>
  )
}
