import { useState } from 'react'
import { useLocale } from '@/lib/i18n/locale-context'
import { useAuth } from './auth-context'
import { AuthForm } from './AuthForm'
import { TextField } from './TextField'
import { useAuthForm } from './use-auth-form'
import { validateLogin } from './validation'

export function LoginPage() {
  const { login } = useAuth()
  const { t } = useLocale()
  const [remember, setRemember] = useState(true)
  const form = useAuthForm(
    ['email', 'password'],
    (values) => validateLogin(values, t.auth.errors),
    (values) => login(values, remember),
  )

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
      <label className="flex w-fit cursor-pointer items-center gap-2.5 text-sm select-none">
        <input
          type="checkbox"
          checked={remember}
          onChange={(event) => setRemember(event.target.checked)}
          className="size-4 cursor-pointer accent-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        />
        {t.auth.fields.rememberMe}
      </label>
    </AuthForm>
  )
}
