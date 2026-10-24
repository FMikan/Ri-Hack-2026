import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { en } from './en'
import { hr } from './hr'
import { LocaleContext, type Locale } from './locale-context'

const STORAGE_KEY = 'locale'
const messages = { en, hr }

// shortcut: the choice lives in localStorage only; sync it with MeDto.locale when the settings screen exists.
function savedLocale(): Locale {
  const saved = localStorage.getItem(STORAGE_KEY)
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
        localStorage.setItem(STORAGE_KEY, next)
        setLocaleState(next)
      },
    }),
    [locale],
  )

  return <LocaleContext value={value}>{children}</LocaleContext>
}
