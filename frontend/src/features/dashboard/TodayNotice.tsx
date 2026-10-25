import { CalendarCheck, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { useLocale } from '@/lib/i18n/locale-context'
import type { DashboardDto } from '@/types/api'

// shortcut: "seen today" lives in this browser's localStorage, so another device shows it once more; move it to the
// server if that starts to matter.
const SEEN_KEY = 'stackoff.today-notice'
const localDay = () => new Date().toLocaleDateString('en-CA') // yyyy-mm-dd in the player's time zone

// The daily tasks live under Practice; this points there on the first dashboard visit of the day while something is
// left to do. It counts as seen as soon as it shows, so it stays for this visit and comes back tomorrow.
export function TodayNotice({ data }: { data: DashboardDto }) {
  const { t } = useLocale()
  const copy = t.dashboard.today.notice
  const [open, setOpen] = useState(() => localStorage.getItem(SEEN_KEY) !== localDay())
  const done = data.dailyTasks.filter((task) => task.completed).length
  const pending = done < data.dailyTasks.length || !data.dailyChallengeAttempted
  const visible = open && data.dailyTasks.length > 0 && pending

  useEffect(() => {
    if (visible) localStorage.setItem(SEEN_KEY, localDay())
  }, [visible])

  if (!visible) return null
  return (
    <section
      aria-labelledby="today-notice"
      className="flex flex-wrap items-center gap-x-4 gap-y-3 rounded-xl bg-card p-4 ring-1 ring-foreground/10 animate-in fade-in slide-in-from-top-2 duration-300 motion-reduce:animate-none"
    >
      <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
        <CalendarCheck aria-hidden="true" className="size-5" />
      </span>
      <div className="grid min-w-0 flex-1 basis-56 gap-0.5">
        <h2 id="today-notice" className="font-medium">
          {copy.title}
        </h2>
        <p className="text-sm text-pretty text-muted-foreground">{copy.text(done, data.dailyTasks.length, !data.dailyChallengeAttempted)}</p>
      </div>
      <div className="flex items-center gap-2">
        <Button asChild variant="outline">
          <Link to="/practice" onClick={() => setOpen(false)}>
            {copy.open}
          </Link>
        </Button>
        <Button variant="ghost" size="icon" className="size-10" aria-label={t.common.close} onClick={() => setOpen(false)}>
          <X aria-hidden="true" />
        </Button>
      </div>
    </section>
  )
}
