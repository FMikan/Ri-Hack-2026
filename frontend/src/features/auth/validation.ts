import { ApiError } from '@/lib/api-client'
import type { Messages } from '@/lib/i18n/en'

export type AuthField = 'email' | 'username' | 'password'
export type FieldErrors = Partial<Record<AuthField, string>>
type ErrorMessages = Messages['auth']['errors']

// Same limits as GameRules and AuthDtos in the backend, checked here so errors are instant and localized.
const EMAIL_MAX = 254
const USERNAME_MIN = 3
const USERNAME_MAX = 20
const USERNAME_PATTERN = /^[A-Za-z0-9_]+$/
const PASSWORD_MIN = 8
const PASSWORD_MAX = 128

function checkEmail(email: string, m: ErrorMessages) {
  if (!email) return m.emailRequired
  if (email.length > EMAIL_MAX) return m.emailTooLong
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return m.emailInvalid
}

export function validateLogin({ email, password }: Record<'email' | 'password', string>, m: ErrorMessages): FieldErrors {
  return { email: checkEmail(email, m), password: password ? undefined : m.passwordRequired }
}

export function validateRegister({ username, email, password }: Record<AuthField, string>, m: ErrorMessages): FieldErrors {
  return {
    username:
      username.length < USERNAME_MIN || username.length > USERNAME_MAX
        ? m.usernameLength
        : USERNAME_PATTERN.test(username)
          ? undefined
          : m.usernameChars,
    email: checkEmail(email, m),
    password: password.length < PASSWORD_MIN || password.length > PASSWORD_MAX ? m.passwordLength : undefined,
  }
}

const isAuthField = (key: string): key is AuthField => key === 'email' || key === 'username' || key === 'password'

// Turns a failed login/register call into messages: per field where the server names one, otherwise for the form.
export function toAuthErrors(error: unknown, t: Messages): { fields: FieldErrors; form?: string } {
  const m = t.auth.errors
  if (!(error instanceof ApiError)) return { fields: {}, form: m.generic }

  switch (error.code) {
    case 'urn:app:auth/invalid-credentials':
      return { fields: {}, form: m.invalidCredentials }
    case 'urn:app:auth/email-taken':
      return { fields: { email: m.emailTaken } }
    case 'urn:app:auth/username-taken':
      return { fields: { username: m.usernameTaken } }
  }

  // Validation keys are C# property names ("Password") and the messages are English only; the checks above
  // mirror the server rules, so this is a fallback.
  const fields: FieldErrors = {}
  for (const [key, messages] of Object.entries(error.problem?.errors ?? {})) {
    const field = key.charAt(0).toLowerCase() + key.slice(1)
    if (isAuthField(field) && messages[0]) fields[field] = messages[0]
  }
  if (Object.keys(fields).length > 0) return { fields }

  if (error.status === 0) return { fields: {}, form: t.common.serverUnreachable }
  return { fields: {}, form: error.status === 429 ? m.tooManyAttempts : m.generic }
}
