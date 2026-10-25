import { useLocale } from '@/lib/i18n/locale-context'
import { useAuth } from './auth-context'
import { AuthForm } from './AuthForm'
import { GoogleSignIn } from './GoogleSignIn'
import { TextField } from './TextField'
import { useAuthForm } from './use-auth-form'
import { validateRegister } from './validation'

export function RegisterPage() {
  const { register } = useAuth()
  const { t } = useLocale()
  const form = useAuthForm(['username', 'email', 'password'], (values) => validateRegister(values, t.auth.errors), register)

  // E-mail sits right before the password so password managers save it as the login name, not the username.
  return (
    <AuthForm copy={t.auth.register} switchTo="/login" beforeForm={<GoogleSignIn remember />} {...form}>
      <TextField
        name="username"
        label={t.auth.fields.username}
        hint={t.auth.hints.username}
        autoComplete="nickname"
        autoCapitalize="none"
        spellCheck={false}
        error={form.errors.username}
      />
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
        hint={t.auth.hints.password}
        autoComplete="new-password"
        error={form.errors.password}
      />
    </AuthForm>
  )
}
