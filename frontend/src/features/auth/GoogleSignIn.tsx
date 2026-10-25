import { useEffect, useRef, useState } from 'react'
import { useAsync } from '@/hooks/use-async'
import { api, ApiError } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import { useAuth } from './auth-context'
import { toAuthErrors } from './validation'

// The part of Google Identity Services (accounts.google.com/gsi/client) this page uses.
interface GoogleIdentity {
  initialize: (config: { client_id: string; callback: (response: { credential: string }) => void }) => void
  renderButton: (
    parent: HTMLElement,
    options: { theme: string; size: string; text: string; shape: string; width: number; locale: string },
  ) => void
}

declare global {
  interface Window {
    google?: { accounts: { id: GoogleIdentity } }
  }
}

// Google warns when initialize() runs twice, so the script loads and initializes once per page load and the mounted
// button swaps in its own handler. A failed load is forgotten so the next mount tries again.
let google: Promise<GoogleIdentity> | undefined
let onCredential: (credential: string) => void = () => {}

function loadGoogle(clientId: string) {
  google ??= new Promise((resolve, reject) => {
    const script = document.createElement('script')
    const fail = () => {
      script.remove()
      google = undefined
      reject(new Error('Google sign-in did not load.'))
    }
    script.src = 'https://accounts.google.com/gsi/client'
    script.async = true
    script.onerror = fail
    script.onload = () => {
      const id = window.google?.accounts.id
      if (!id) return fail()
      id.initialize({ client_id: clientId, callback: ({ credential }) => onCredential(credential) })
      resolve(id)
    }
    document.head.append(script)
  })
  return google
}

// "Continue with Google" and an "or" divider above the e-mail form. Renders nothing while Google sign-in is off,
// the config call failed or the script was blocked. On success the route guard navigates away, like after login.
export function GoogleSignIn({ remember }: { remember: boolean }) {
  const { googleLogin } = useAuth()
  const { t, locale } = useLocale()
  const clientId = useAsync(api.authConfig).data?.googleClientId
  const buttonRef = useRef<HTMLDivElement>(null)
  const [unavailable, setUnavailable] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()

  // Swapped in after every render so the handler sees the current "Remember me" and language.
  useEffect(() => {
    onCredential = async (credential) => {
      setError(undefined)
      setBusy(true)
      try {
        await googleLogin(credential, remember)
      } catch (failure) {
        setError(
          failure instanceof ApiError && failure.code === 'urn:app:auth/email-taken'
            ? t.auth.errors.googleEmailTaken
            : (toAuthErrors(failure, t).form ?? t.auth.errors.generic),
        )
      } finally {
        setBusy(false)
      }
    }
  })

  // Google draws its own button (an iframe) into the container, again whenever the language changes.
  useEffect(() => {
    const container = buttonRef.current
    if (!clientId || !container) return
    let active = true
    loadGoogle(clientId).then(
      (id) =>
        active &&
        id.renderButton(container, {
          theme: 'filled_black',
          size: 'large',
          text: 'continue_with',
          shape: 'rectangular',
          width: Math.min(container.offsetWidth, 400),
          locale,
        }),
      () => active && setUnavailable(true),
    )
    return () => {
      active = false
    }
  }, [clientId, locale])

  if (!clientId || unavailable) return null

  return (
    <div className="mt-6 grid gap-4 sm:mt-8 sm:gap-5 short:mt-4 short:gap-3">
      {error && (
        <p role="alert" className="rounded-lg bg-destructive/10 px-3 py-2.5 text-sm text-destructive">
          {error}
        </p>
      )}
      <div className="relative min-h-10">
        <div ref={buttonRef} className={cn(busy && 'invisible')} />
        {busy && (
          <p role="status" className="absolute inset-0 grid place-items-center rounded-md bg-secondary text-sm font-medium">
            {t.auth.google.busy}
          </p>
        )}
      </div>
      <p className="flex items-center gap-3 text-xs text-muted-foreground before:h-px before:flex-1 before:bg-input/40 after:h-px after:flex-1 after:bg-input/40">
        {t.auth.google.or}
      </p>
    </div>
  )
}
