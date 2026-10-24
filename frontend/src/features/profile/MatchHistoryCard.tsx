import { createElement } from 'react'
import { Link } from 'react-router'
import { BotAvatar } from '@/components/shared/BotAvatar'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import { cn } from '@/lib/utils'
import type { MatchSummaryDto } from '@/types/api'

const OUTCOMES: Record<MatchSummaryDto['outcome'], string> = {
  win: 'bg-success/15 text-success',
  loss: 'bg-destructive/15 text-destructive',
  draw: 'bg-secondary text-muted-foreground',
  aborted: 'bg-secondary text-muted-foreground',
  completed: 'bg-primary/15 text-primary',
}

// Last 50 finished matches. On wide screens the card takes the height of its row and the list scrolls inside.
export function MatchHistoryCard({ history, className }: { history: MatchSummaryDto[]; className?: string }) {
  const { t, locale } = useLocale()
  const copy = t.profile.history
  const opponentName = (match: MatchSummaryDto) =>
    !match.opponent
      ? (match.topic?.name ?? copy.practice)
      : match.opponent.persona
        ? t.bots[match.opponent.persona]
        : (match.opponent.user?.username ?? '')

  return (
    <Card className={cn('xl:[contain:size]', className)}>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>{copy.subtitle}</CardDescription>
      </CardHeader>
      <CardContent className="flex min-h-0 flex-1 flex-col">
        {history.length === 0 ? (
          <div className="grid justify-items-start gap-3">
            <p className="text-sm text-muted-foreground">{copy.empty}</p>
            <Button asChild variant="outline">
              <Link to="?play=bot" replace>
                {copy.play}
              </Link>
            </Button>
          </div>
        ) : (
          <ul className="grid max-h-96 min-h-0 flex-1 content-start gap-2 overflow-y-auto [scrollbar-width:thin] xl:max-h-none">
            {history.map((match) => (
              <li key={match.id} className="flex items-center gap-3 rounded-lg px-3 py-2.5 ring-1 ring-border ring-inset">
                <OpponentAvatar match={match} />
                <span className="grid min-w-0 flex-1">
                  <span className="truncate text-sm font-medium">{opponentName(match)}</span>
                  <span className="truncate text-xs text-muted-foreground">
                    {[match.opponent && match.topic?.name, match.finishedAt && relativeTime(match.finishedAt, locale)]
                      .filter(Boolean)
                      .join(' · ')}
                  </span>
                </span>
                <span className="grid shrink-0 justify-items-end gap-1">
                  <span className="font-mono text-sm tabular-nums">
                    {match.opponent ? `${match.myScore} : ${match.opponentScore}` : match.myScore}
                  </span>
                  <span className={cn('rounded-full px-2 py-0.5 text-xs font-medium', OUTCOMES[match.outcome])}>
                    {copy.outcomes[match.outcome]}
                  </span>
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

// The bot's portrait, a person's initial, or the topic for practice (no opponent).
function OpponentAvatar({ match }: { match: MatchSummaryDto }) {
  const avatar = 'grid size-10 shrink-0 place-items-center rounded-full'
  if (match.opponent?.persona) return <BotAvatar persona={match.opponent.persona} className="size-10" />
  if (match.opponent)
    return (
      <span aria-hidden="true" className={cn(avatar, 'bg-secondary font-heading font-semibold ring-1 ring-border')}>
        {match.opponent.user?.username.charAt(0).toUpperCase()}
      </span>
    )
  return (
    <span aria-hidden="true" className={cn(avatar, 'bg-primary/15 text-primary')}>
      {createElement(topicIcon(match.topic?.slug ?? ''), { className: 'size-5' })}
    </span>
  )
}
