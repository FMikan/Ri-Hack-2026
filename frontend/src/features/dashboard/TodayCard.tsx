import { Check, ChevronRight, ListChecks, RotateCcw, Sparkles, Swords, Target, Timer, type LucideIcon } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import coinSmall from '@/assets/brand/stackoff-coin-small-on-dark.svg'
import { Meter } from '@/components/shared/Meter'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useStartMatch } from '@/features/match/use-start-match'
import { api } from '@/lib/api-client'
import { DAILY_BONUS, DAY_TIME_ZONE } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { DailyTaskDto, DashboardDto } from '@/types/api'

// Where each daily task is done (docs §6.11 F4b). practice_weakest starts that practice directly.
const TASK_LINKS: Record<string, string> = {
  play_2_ranked: '/?play=ranked',
  daily_challenge: '/daily',
  '3_brilliant': '/?play=bot',
  review_all_due: '/practice',
}

const TASK_ICONS: Record<string, LucideIcon> = {
  play_2_ranked: Swords,
  daily_challenge: Timer,
  '3_brilliant': Sparkles,
  review_all_due: RotateCcw,
  practice_weakest: Target,
}

// lg: one column per tile, and the card keeps a third of the width per tile, so two tiles are as wide as with three.
const COLUMNS: Record<number, string> = { 1: 'lg:grid-cols-1', 2: 'lg:grid-cols-2', 3: 'lg:grid-cols-3', 4: 'lg:grid-cols-4' }
const WIDTHS: Record<number, string> = { 1: 'lg:max-w-[calc(100%/3)]', 2: 'lg:max-w-[calc(200%/3)]' }

const tile = 'relative grid content-between gap-3 rounded-lg p-4 ring-1 ring-inset'
// The whole tile is the target: the title is the link or button and its ::after covers the tile, so the accessible
// name stays the title and the focus ring shows on the tile.
const stretch = 'text-left outline-none after:absolute after:inset-0 after:rounded-lg'
const hoverable =
  'group/tile transition-colors has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-ring motion-reduce:transition-none'

// Seconds to the next midnight where tasks and the challenge reset (GameRules.DayTimeZone). DST flips at 2 or 3 am,
// after midnight, so the wall clock gives the right count.
function secondsToReset(now: Date) {
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone: DAY_TIME_ZONE, hourCycle: 'h23', hour: 'numeric', minute: 'numeric', second: 'numeric' }).formatToParts(now)
  const part = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find((entry) => entry.type === type)?.value ?? 0)
  return 24 * 3600 - part('hour') * 3600 - part('minute') * 60 - part('second')
}

// Today's three tasks (20 coins each, all three 30 more) and the daily challenge. When the challenge is one of the
// tasks, the two share one tile.
export function TodayCard({ data, className }: { data: DashboardDto; className?: string }) {
  const { t } = useLocale()
  const copy = t.dashboard.today
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const id = window.setInterval(() => setNow(new Date()), 30_000) // keeps the reset countdown current
    return () => window.clearInterval(id)
  }, [])
  const minutesLeft = Math.ceil(secondsToReset(now) / 60)
  const total = data.dailyTasks.length
  const done = data.dailyTasks.filter((task) => task.completed).length
  const challengeTask = data.dailyTasks.find((task) => task.key === 'daily_challenge')
  const tiles = data.dailyTasks.filter((task) => task !== challengeTask).length + 1 // the challenge always has a tile

  return (
    <Card className={cn(WIDTHS[tiles], className)}>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>{copy.resets(Math.floor(minutesLeft / 60), minutesLeft % 60)}</CardDescription>
        {total > 0 && (
          <CardAction className="flex items-center gap-2 text-xs">
            <span aria-hidden="true" className="flex gap-1">
              {data.dailyTasks.map((task) => (
                <span key={task.key} className={cn('size-2 rounded-full', task.completed ? 'bg-success' : 'bg-foreground/15')} />
              ))}
            </span>
            <span className="font-mono tabular-nums">
              {done}/{total}
            </span>
            <span aria-hidden="true" className="text-muted-foreground">
              ·
            </span>
            <span className={cn('inline-flex items-center gap-1', done === total ? 'text-success' : 'text-muted-foreground')}>
              {copy.bonus(DAILY_BONUS)}
              <Coin />
            </span>
          </CardAction>
        )}
      </CardHeader>
      <CardContent className={cn('grid gap-3', COLUMNS[tiles])}>
        {data.dailyTasks
          .filter((task) => task !== challengeTask)
          .map((task) => (
            <Task key={task.key} task={task} />
          ))}
        <Challenge task={challengeTask} attempted={data.dailyChallengeAttempted} />
      </CardContent>
    </Card>
  )
}

function Task({ task }: { task: DailyTaskDto }) {
  const { t } = useLocale()
  const copy = t.dashboard.today
  const { start, starting } = useStartMatch()
  const text = copy.tasks[task.key]
  const label = typeof text === 'function' ? text(task.topic?.name ?? '') : (text ?? task.key)
  const to = TASK_LINKS[task.key]
  const topicId = task.topic?.id
  const Icon = TASK_ICONS[task.key] ?? ListChecks
  const action = task.completed ? undefined : task.key === 'practice_weakest' && topicId ? (
    <button
      type="button"
      className={stretch}
      aria-disabled={starting}
      onClick={() => !starting && start(() => api.startPractice({ topicId, timed: false }))}
    >
      {label}
    </button>
  ) : to ? (
    <Link to={to} replace={to.startsWith('/?')} className={stretch}>
      {label}
    </Link>
  ) : undefined

  return (
    <div
      className={cn(
        tile,
        task.completed
          ? 'bg-success/10 ring-success/40'
          : cn('bg-secondary ring-border', action && cn(hoverable, 'hover:bg-[color-mix(in_oklch,var(--secondary),var(--foreground)_5%)]')),
      )}
    >
      <div className="flex items-start gap-3">
        <span
          className={cn(
            'grid size-8 shrink-0 place-items-center rounded-lg',
            task.completed ? 'bg-success/15 text-success' : 'bg-primary/10 text-primary',
          )}
        >
          <Icon aria-hidden="true" className="size-4" />
        </span>
        <span className="min-w-0 flex-1 self-center text-sm font-medium text-pretty">{action ?? label}</span>
        {task.completed && <Done />}
      </div>
      <Progress task={task} label={label} chevron={!!action} />
    </div>
  )
}

function Challenge({ task, attempted }: { task?: DailyTaskDto; attempted: boolean }) {
  const { t } = useLocale()
  const copy = t.dashboard.today

  return (
    <div className={cn(tile, hoverable, 'bg-primary/10 ring-primary/40 hover:bg-primary/15')}>
      <div className="flex items-start gap-3">
        <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-primary/15 text-primary">
          <Timer aria-hidden="true" className="size-4" />
        </span>
        <span className="grid min-w-0 flex-1 gap-0.5 self-center">
          <Link to="/daily" className={cn(stretch, 'text-sm font-medium')}>
            {copy.challenge}
          </Link>
          <span className="text-xs text-muted-foreground">{attempted ? copy.challengeDone : copy.challengeOpen}</span>
        </span>
        {task?.completed && <Done />}
      </div>
      {task ? (
        <Progress task={task} label={copy.challenge} chevron />
      ) : (
        <Chevron className="justify-self-end text-muted-foreground" />
      )}
    </div>
  )
}

// The bar, "1/3" and the reward, with the arrow when the tile leads somewhere. The track is lifted off the tile.
function Progress({ task, label, chevron }: { task: DailyTaskDto; label: string; chevron: boolean }) {
  const { t } = useLocale()

  return (
    <div className="grid gap-2">
      <Meter value={task.progress / task.target} label={label} className="bg-foreground/10" />
      <p className="flex items-center justify-between gap-2 text-xs text-muted-foreground">
        <span className="font-mono tabular-nums">
          {Math.min(task.progress, task.target)}/{task.target}
        </span>
        <span className="flex items-center gap-2">
          <span className="inline-flex items-center gap-1 font-mono tabular-nums">
            {t.dashboard.today.reward(task.reward)}
            <Coin />
          </span>
          {chevron && <Chevron />}
        </span>
      </p>
    </div>
  )
}

function Coin() {
  const { t } = useLocale()
  return <img src={coinSmall} alt={t.shell.coins} className="size-3.5" />
}

function Chevron({ className }: { className?: string }) {
  return (
    <ChevronRight
      aria-hidden="true"
      className={cn('size-4 transition-transform group-hover/tile:translate-x-0.5 motion-reduce:transition-none', className)}
    />
  )
}

function Done() {
  const { t } = useLocale()
  return (
    <span className="flex items-center gap-1 text-xs font-medium text-success">
      <Check aria-hidden="true" className="size-3.5" />
      {t.dashboard.today.done}
    </span>
  )
}
