import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { en } from './en'
import { hr } from './hr'
import { LocaleContext, type Locale } from './locale-context'

const STORAGE_KEY = 'locale'
const messages = { en, hr }

// The app always opens in English; a picked language lasts only for the tab (sessionStorage), so reloads keep it
// but a fresh open does not. Sync it with MeDto.locale when the settings screen exists.
function savedLocale(): Locale {
  const saved = sessionStorage.getItem(STORAGE_KEY)
  return saved === 'hr' ? 'hr' : 'en'
}

export function LocaleProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState(savedLocale)

  useEffect(() => {
    document.documentElement.lang = locale
  }, [locale])

  const value = useMemo(
    () => ({
      locale,
      t: messages[locale],
      setLocale: (next: Locale) => {
        sessionStorage.setItem(STORAGE_KEY, next)
        setLocaleState(next)
      },
    }),
    [locale],
  )

  return <LocaleContext value={value}>{children}</LocaleContext>
}
