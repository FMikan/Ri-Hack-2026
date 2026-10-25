import type { Locale } from '@/lib/i18n/locale-context'

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 86_400_000],
  ['month', 30 * 86_400_000],
  ['week', 7 * 86_400_000],
  ['day', 86_400_000],
  ['hour', 3_600_000],
  ['minute', 60_000],
]

// "3 days ago", "in 2 hours" ("prije 3 dana", "za 2 sata"); under a minute reads as "now".
export function relativeTime(iso: string, locale: Locale, now = Date.now()) {
  const diff = Date.parse(iso) - now
  const format = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' })
  const [unit, size] = UNITS.find(([, size]) => Math.abs(diff) >= size) ?? ['second', 1000]
  return format.format(unit === 'second' ? 0 : Math.round(diff / size), unit)
}

// 1:05 for a running wait (the ranked queue, the professor writing a lesson).
export const clock = (ms: number) => {
  const seconds = Math.floor(ms / 1000)
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`
}

export const monthYear = (iso: string, locale: Locale) =>
  new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric' }).format(new Date(iso))
