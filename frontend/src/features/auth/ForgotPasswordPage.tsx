import { useState } from 'react'
import { Link } from 'react-router'
import { api } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import { AuthForm } from './AuthForm'
import { AuthLayout } from './AuthLayout'
import { TextField } from './TextField'
import { useAuthForm } from './use-auth-form'
import { validateEmail } from './validation'

// The API answers 202 whether or not the account exists, so the page never says which (docs §6.11 F0b).
export function ForgotPasswordPage() {
  const { t } = useLocale()
  const copy = t.auth.forgot
  const [sentTo, setSentTo] = useState<string>()
  const form = useAuthForm(['email'], (values) => validateEmail(values, t.auth.errors), async ({ email }) => {
    await api.forgotPassword({ email })
    setSentTo(email)
  })

  if (sentTo)
    return (
      <AuthLayout title={copy.documentTitle}>
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-balance sm:text-3xl">{copy.sentTitle}</h1>
        <p role="status" className="mt-2 text-pretty text-muted-foreground">
          {copy.sent(sentTo)}
        </p>
        <Link to="/login" className="mt-6 inline-block rounded-sm font-medium underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
          {copy.switchLink}
        </Link>
      </AuthLayout>
    )

  return (
    <AuthForm copy={copy} switchTo="/login" {...form}>
      <TextField
        name="email"
        type="email"
        label={t.auth.fields.email}
        autoComplete="email"
        autoCapitalize="none"
        spellCheck={false}
        error={form.errors.email}
      />
    </AuthForm>
  )
}
