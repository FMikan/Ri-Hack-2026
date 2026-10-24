import { Check, ChevronRight, Timer } from 'lucide-react'
import { Link } from 'react-router'
import { Meter } from '@/components/shared/Meter'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useStartMatch } from '@/features/match/use-start-match'
import { api } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { DailyTaskDto, DashboardDto } from '@/types/api'

// Where each daily task is done (docs §6.11 F4b). practice_weakest starts that practice directly.
const TASK_LINKS: Record<string, string> = {
  play_2_ranked: '/?play=ranked',
  daily_challenge: '/daily',
  '3_brilliant': '/?play=bot',
  review_all_due: '/practice',
  patch_1_concept: '/profile',
}

// Today's three tasks (20 coins each, all three 30 more) and the daily challenge.
export function TodayCard({ data, className }: { data: DashboardDto; className?: string }) {
  const { t } = useLocale()
  const copy = t.dashboard.today

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>{copy.subtitle}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3 lg:grid-cols-4">
        {data.dailyTasks.map((task) => (
          <Task key={task.key} task={task} />
        ))}
        <Link
          to="/daily"
          className="group/daily grid content-between gap-3 rounded-lg bg-primary/10 p-4 ring-1 ring-primary/40 transition-colors ring-inset hover:bg-primary/15 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <span className="flex items-center gap-2 font-medium">
            <Timer aria-hidden="true" className="size-4 text-primary" />
            {copy.challenge}
          </span>
          <span className="flex items-center justify-between gap-2 text-sm text-muted-foreground">
            {data.dailyChallengeAttempted ? copy.challengeDone : copy.challengeOpen}
            <ChevronRight aria-hidden="true" className="size-4 transition-transform group-hover/daily:translate-x-0.5 motion-reduce:transition-none" />
          </span>
        </Link>
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

  return (
    <div className={cn('grid content-between gap-3 rounded-lg p-4 ring-1 ring-inset', task.completed ? 'bg-success/10 ring-success/40' : 'bg-secondary ring-border')}>
      <div className="flex items-start gap-2">
        <span className="min-w-0 flex-1 text-sm font-medium text-pretty">{label}</span>
        {task.completed && (
          <span className="flex items-center gap-1 text-xs font-medium text-success">
            <Check aria-hidden="true" className="size-3.5" />
            {copy.done}
          </span>
        )}
      </div>
      <div className="grid gap-2">
        <Meter value={task.progress / task.target} label={label} />
        <p className="flex items-center justify-between gap-2 text-xs text-muted-foreground">
          <span className="font-mono tabular-nums">
            {Math.min(task.progress, task.target)}/{task.target} · {copy.reward(task.reward)}
          </span>
          {!task.completed &&
            (task.key === 'practice_weakest' && topicId ? (
              <Button
                variant="ghost"
                size="sm"
                className="-my-1 h-7"
                aria-disabled={starting}
                onClick={() => start(() => api.startPractice({ topicId, timed: false }))}
              >
                {copy.go}
              </Button>
            ) : (
              to && (
                <Button asChild variant="ghost" size="sm" className="-my-1 h-7">
                  <Link to={to} replace={to.startsWith('/?')}>
                    {copy.go}
                  </Link>
                </Button>
              )
            ))}
        </p>
      </div>
    </div>
  )
}
