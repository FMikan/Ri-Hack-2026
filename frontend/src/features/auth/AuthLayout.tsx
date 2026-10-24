import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { LocaleSwitch } from '@/components/shared/LocaleSwitch'
import { Wordmark } from '@/components/shared/Wordmark'
import { useLocale } from '@/lib/i18n/locale-context'
import { DuelPreview } from './DuelPreview'

// Form on the left, a slate brand panel with a still of a duel on the right (hidden below lg).
// Fits one screen from about 375x550 up; smaller viewports fall back to page scroll rather than clipping the form.
export function AuthLayout({ title, children }: { title: string; children: ReactNode }) {
  const { t } = useLocale()

  return (
    <div className="grid min-h-svh gap-4 p-4 lg:grid-cols-2">
      <title>{`${title} · StackOff`}</title>

      <div className="flex min-w-0 flex-col sm:px-6 lg:px-10">
        <header className="flex items-center justify-between gap-4">
          <Link
            to="/"
            className="rounded-sm focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
          >
            <Wordmark />
          </Link>
          <LocaleSwitch />
        </header>
        <main className="flex flex-1 items-center justify-center py-6 short:py-3">
          <div className="w-full max-w-sm">{children}</div>
        </main>
      </div>

      {/* contain:size: the row height comes from the form, so on short screens the preview is clipped instead of
          the page growing a scrollbar. */}
      <aside className="hidden min-w-0 flex-col justify-between gap-8 overflow-hidden rounded-2xl bg-card p-10 short:gap-6 short:p-8 lg:flex lg:[contain:size]">
        <div className="grid max-w-md gap-4">
          <h2 className="font-heading text-4xl font-semibold tracking-tight text-balance short:text-3xl">
            {t.auth.panel.title}
          </h2>
          <p className="text-lg text-pretty text-muted-foreground short:hidden">{t.auth.panel.body}</p>
        </div>
        <DuelPreview />
        <p className="font-mono text-xs font-medium text-muted-foreground">{t.auth.panel.facts}</p>
      </aside>
    </div>
  )
}
