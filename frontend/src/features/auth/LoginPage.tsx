import { useLocale } from '@/lib/i18n/locale-context'
import { useAuth } from './auth-context'
import { AuthForm } from './AuthForm'
import { TextField } from './TextField'
import { useAuthForm } from './use-auth-form'
import { validateLogin } from './validation'

export function LoginPage() {
  const { login } = useAuth()
  const { t } = useLocale()
  const form = useAuthForm(['email', 'password'], (values) => validateLogin(values, t.auth.errors), login)

  return (
    <AuthForm copy={t.auth.login} switchTo="/register" {...form}>
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
    </AuthForm>
  )
}
