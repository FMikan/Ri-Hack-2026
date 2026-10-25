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

// "Continue with Google" and an "or" divider above the e-mail form. Until a client id is configured (or while the
// config loads, or when Google's script is blocked) our own look-alike button stands in and says it is coming soon.
// On success the route guard navigates away, like after login.
export function GoogleSignIn({ remember }: { remember: boolean }) {
  const { googleLogin } = useAuth()
  const { t, locale } = useLocale()
  const clientId = useAsync(api.authConfig).data?.googleClientId
  const buttonRef = useRef<HTMLDivElement>(null)
  const [unavailable, setUnavailable] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()
  const [soon, setSoon] = useState(false)
  const ready = !!clientId && !unavailable

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

  return (
    <div className="mt-6 grid gap-4 sm:mt-8 sm:gap-5 short:mt-4 short:gap-3">
      {error && (
        <p role="alert" className="rounded-lg bg-destructive/10 px-3 py-2.5 text-sm text-destructive">
          {error}
        </p>
      )}
      {soon && !ready && (
        <p role="status" className="rounded-lg bg-secondary px-3 py-2.5 text-sm">
          {t.auth.google.soon}
        </p>
      )}
      <div className="relative min-h-10">
        {ready ? (
          <div ref={buttonRef} className={cn(busy && 'invisible')} />
        ) : (
          <button
            type="button"
            onClick={() => setSoon(true)}
            className="flex h-10 w-full max-w-100 items-center justify-center gap-3 rounded-[4px] bg-[#131314] px-3 text-sm font-medium text-[#e3e3e3] ring-1 ring-[#8e918f] ring-inset transition-colors hover:bg-[#1f1f20] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            <GoogleLogo />
            {t.auth.google.continue}
          </button>
        )}
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

// Google's four-colour "G", as its own sign-in button draws it.
function GoogleLogo() {
  return (
    <svg aria-hidden="true" viewBox="0 0 48 48" className="size-[18px] shrink-0">
      <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
      <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
      <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
      <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
    </svg>
  )
}
