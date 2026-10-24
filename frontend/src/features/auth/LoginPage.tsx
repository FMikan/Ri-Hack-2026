import { Link, useLocation } from 'react-router'
import { useLocale } from '@/lib/i18n/locale-context'
import { useAuth } from './auth-context'
import { AuthForm } from './AuthForm'
import { TextField } from './TextField'
import { useAuthForm } from './use-auth-form'
import { validateLogin } from './validation'

export function LoginPage() {
  const { login } = useAuth()
  const { t } = useLocale()
  const location = useLocation()
  const form = useAuthForm(['email', 'password'], (values) => validateLogin(values, t.auth.errors), login)
  const passwordReset = (location.state as { notice?: string } | null)?.notice === 'password-reset'

  return (
    <AuthForm copy={t.auth.login} switchTo="/register" {...form}>
      {passwordReset && (
        <p role="status" className="rounded-lg bg-success/10 px-3 py-2.5 text-sm text-success">
          {t.auth.reset.done}
        </p>
      )}
      <TextField
        name="email"
        type="email"
        label={t.auth.fields.email}
        autoComplete="email"
        autoCapitalize="none"
        spellCheck={false}
        error={form.errors.email}
      />
      <TextField
        name="password"
        type="password"
        label={t.auth.fields.password}
        autoComplete="current-password"
        error={form.errors.password}
      />
      <Link
        to="/forgot-password"
        className="-mt-2 justify-self-end rounded-sm text-sm text-muted-foreground underline-offset-4 transition-colors hover:text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        {t.auth.login.forgot}
      </Link>
    </AuthForm>
  )
}
