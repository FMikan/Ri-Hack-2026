import { createContext, useContext } from 'react'
import { en, type Messages } from './en'

export type Locale = 'en' | 'hr'

export const LOCALE_NAMES: Record<Locale, string> = { en: 'English', hr: 'Hrvatski' }

export interface LocaleState {
  locale: Locale
  setLocale: (locale: Locale) => void
  t: Messages
}

export const LocaleContext = createContext<LocaleState>({ locale: 'en', setLocale: () => {}, t: en })

// All UI copy comes from `t`; never hardcode user-facing text in components.
export const useLocale = () => useContext(LocaleContext)
