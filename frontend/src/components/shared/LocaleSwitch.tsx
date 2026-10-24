import { LOCALE_NAMES, useLocale, type Locale } from '@/lib/i18n/locale-context'

const locales = Object.keys(LOCALE_NAMES) as Locale[]

export function LocaleSwitch() {
  const { locale, setLocale, t } = useLocale()

  return (
    <div role="group" aria-label={t.common.language} className="flex gap-1 rounded-lg bg-secondary p-1">
      {locales.map((option) => (
        <button
          key={option}
          type="button"
          lang={option}
          aria-label={LOCALE_NAMES[option]}
          aria-pressed={locale === option}
          onClick={() => setLocale(option)}
          className="h-9 min-w-11 rounded-sm px-2 font-mono text-xs font-medium uppercase text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring aria-pressed:bg-background aria-pressed:text-foreground"
        >
          {option}
        </button>
      ))}
    </div>
  )
}
