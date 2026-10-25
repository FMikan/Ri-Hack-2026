import { CalendarCheck, Info, Timer, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import coinSmall from '@/assets/brand/stackoff-coin-small-on-dark.svg'
import { Rewards } from '@/components/shared/Rewards'
import { Button } from '@/components/ui/button'
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { dialogFooter } from '@/features/play/dialog-footer'
import { DAILY_BONUS, DAILY_CHALLENGE_REWARD } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { DashboardDto } from '@/types/api'

// shortcut: "seen today" lives in this browser's localStorage, so another device shows it once more; move it to the
// server if that starts to matter.
const SEEN_KEY = 'stackoff.today-notice'
const localDay = () => new Date().toLocaleDateString('en-CA') // yyyy-mm-dd in the player's time zone

type Stage = 'prompt' | 'notice' | 'closed'

// The first dashboard visit of the day: a prompt for the daily challenge while it is open (unless an invite is
// waiting, so two asks never stack), otherwise or after "later" a quiet notice that today's tasks wait under Practice.
// It counts as seen as soon as either shows, so it stays for this visit and comes back tomorrow.
export function TodayNotice({ data }: { data: DashboardDto }) {
  const [stage, setStage] = useState<Stage>(() => (localStorage.getItem(SEEN_KEY) === localDay() ? 'closed' : 'prompt'))
  const done = data.dailyTasks.filter((task) => task.completed).length
  const pending = done < data.dailyTasks.length || !data.dailyChallengeAttempted
  const invited = !!(data.inbox.rematch || data.inbox.friendChallenge)
  const prompt = stage === 'prompt' && !data.dailyChallengeAttempted && !invited
  const notice = !prompt && stage !== 'closed' && data.dailyTasks.length > 0 && pending

  useEffect(() => {
    if (prompt || notice) localStorage.setItem(SEEN_KEY, localDay())
  }, [prompt, notice])

  if (prompt) return <ChallengePrompt data={data} done={done} onLater={() => setStage('notice')} onStart={() => setStage('closed')} />
  if (notice) return <Notice data={data} done={done} onClose={() => setStage('closed')} />
  return null
}

interface ChallengePromptProps {
  data: DashboardDto
  done: number
  onLater: () => void
  onStart: () => void
}

function ChallengePrompt({ data, done, onLater, onStart }: ChallengePromptProps) {
  const { t } = useLocale()
  const copy = t.dashboard.today.prompt
  const total = data.dailyTasks.length

  return (
    <Dialog open onOpenChange={(open) => !open && onLater()}>
      <DialogContent showCloseButton={false} className="gap-5 bg-card p-5 sm:max-w-md sm:p-6">
        <DialogHeader className="flex-row items-start gap-3 text-left">
          <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
            <Timer aria-hidden="true" className="size-5" />
          </span>
          <div className="grid flex-1 gap-1">
            <DialogTitle className="font-heading text-xl font-semibold tracking-tight">{copy.title}</DialogTitle>
            <DialogDescription className="text-pretty">{copy.body}</DialogDescription>
          </div>
          <DialogClose asChild>
            <Button variant="ghost" size="icon" aria-label={t.common.close} className="-mt-1 -mr-2 shrink-0">
              <X aria-hidden="true" />
            </Button>
          </DialogClose>
        </DialogHeader>

        <div className="flex flex-wrap items-center gap-3">
          <span className="text-xs text-muted-foreground">{copy.rewardFor}</span>
          <Rewards coins={DAILY_CHALLENGE_REWARD.coins} xp={DAILY_CHALLENGE_REWARD.xp} className="justify-start" />
        </div>

        {total > 0 && (
          <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1 rounded-lg bg-secondary px-3 py-2.5 text-xs ring-1 ring-border ring-inset">
            <span aria-hidden="true" className="flex gap-1">
              {data.dailyTasks.map((task) => (
                <span key={task.key} className={cn('size-2 rounded-full', task.completed ? 'bg-success' : 'bg-foreground/15')} />
              ))}
            </span>
            <span className="font-medium">{copy.tasks}</span>
            <span className="font-mono tabular-nums">
              {done}/{total}
            </span>
            <span className="ml-auto inline-flex items-center gap-1 text-muted-foreground">
              {t.dashboard.today.bonus(DAILY_BONUS)}
              <img src={coinSmall} alt={t.shell.coins} className="size-3.5" />
            </span>
          </div>
        )}

        {/* Opening the challenge starts its clock (GET /api/daily-challenge), so the player hears it before the click. */}
        <p className="flex items-start gap-2 text-xs text-muted-foreground">
          <Info aria-hidden="true" className="mt-px size-3.5 shrink-0" />
          {copy.clock}
        </p>

        <div className={dialogFooter}>
          <Button variant="ghost" size="lg" onClick={onLater}>
            {copy.later}
          </Button>
          <Button asChild size="lg">
            <Link to="/daily" onClick={onStart}>
              <Timer aria-hidden="true" />
              {copy.start}
            </Link>
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}

function Notice({ data, done, onClose }: { data: DashboardDto; done: number; onClose: () => void }) {
  const { t } = useLocale()
  const copy = t.dashboard.today.notice

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
          <Link to="/practice" onClick={onClose}>
            {copy.open}
          </Link>
        </Button>
        <Button variant="ghost" size="icon" className="size-10" aria-label={t.common.close} onClick={onClose}>
          <X aria-hidden="true" />
        </Button>
      </div>
    </section>
  )
}
