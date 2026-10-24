import { useState } from 'react'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuth } from '@/features/auth/auth-context'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { demoMe } from '@/lib/fixtures/demo'
import { demoLeaderboard, type LeaderboardBoard, type LeaderboardEntryDto } from '@/lib/fixtures/leaderboards'
import { relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'

const BOARDS: LeaderboardBoard[] = ['global', 'season', 'weekly', 'topic', 'friends']
const MEDALS = ['bg-medal-gold', 'bg-medal-silver', 'bg-medal-bronze']

// The five leaderboards from the game idea (§14). They run on sample players until the backend serves them (F5).
export function LeaderboardsPage() {
  const { t, locale } = useLocale()
  const copy = t.leaderboards
  const { session } = useAuth()
  const topics = useAsync(api.listTopics)
  const [board, setBoard] = useState<LeaderboardBoard>('global')
  const [topicSlug, setTopicSlug] = useState<string>()
  if (session.status !== 'authenticated') return null

  const me = demoMe(session.user)
  const slug = topicSlug ?? topics.data?.[0]?.slug ?? ''
  const data = demoLeaderboard(board, me, board === 'topic' ? slug : '')
  const podium = data.entries.slice(0, 3)
  const rest = data.entries.slice(3)
  // Only when I am outside the list (friends always include me, highlighted in place).
  const around = data.me?.around.length ? data.me : undefined

  return (
    <main className="mx-auto grid w-full max-w-7xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${copy.documentTitle} · StackOff`}</title>
      <header className="grid gap-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{copy.title}</h1>
        <p className="text-sm text-pretty text-muted-foreground">{copy.subtitle}</p>
      </header>

      <div className="grid gap-3">
        <SegmentedControl
          label={copy.board}
          name="board"
          value={board}
          onChange={setBoard}
          options={BOARDS.map((value) => ({ value, label: copy.boards[value] }))}
          className="justify-self-start"
        />
        {board === 'topic' && topics.data && (
          <SegmentedControl
            label={copy.topic}
            name="board-topic"
            value={slug}
            onChange={setTopicSlug}
            options={topics.data.map((topic) => ({ value: topic.slug, label: topic.name }))}
            className="justify-self-start"
          />
        )}
        <p className="text-sm text-muted-foreground">
          {copy.hints[board]}
          {data.resetsAt && <span className="font-mono text-xs"> · {copy.resets(relativeTime(data.resetsAt, locale))}</span>}
        </p>
      </div>

      <ol className="grid grid-cols-3 gap-3 sm:gap-4 lg:gap-5">
        {podium.map((entry, index) => (
          <li key={entry.user.id} className="min-w-0">
            <Card className={cn('h-full max-sm:[--card-spacing:--spacing(3)]', entry.user.id === me.id && 'ring-2 ring-primary')}>
              <CardContent className="grid justify-items-center gap-2 text-center">
                <span
                  className={cn('grid size-8 place-items-center rounded-full font-mono text-sm font-semibold text-background', MEDALS[index])}
                >
                  {entry.rank}
                </span>
                <UserAvatar user={entry.user} className="size-12 sm:size-14" />
                <p className="line-clamp-2 w-full text-xs font-medium [overflow-wrap:anywhere] sm:text-sm">{entry.user.username}</p>
                <p className="hidden text-xs text-muted-foreground sm:block">{t.dashboard.tiers[entry.user.tier]}</p>
                <p className="font-heading text-xl font-semibold tabular-nums sm:text-3xl">{entry.value}</p>
                <p className="text-xs text-muted-foreground">{copy.values[board]}</p>
              </CardContent>
            </Card>
          </li>
        ))}
      </ol>

      <div className="grid items-start gap-3 sm:gap-4 lg:gap-5 xl:grid-cols-3">
        {rest.length > 0 && (
          <Card className={around ? 'xl:col-span-2' : 'xl:col-span-3'}>
            <CardContent>
              <Rows entries={rest} meId={me.id} valueLabel={copy.values[board]} />
            </CardContent>
          </Card>
        )}
        {around && (
          // On phones my own row comes before the long list; on wide screens it sits beside it.
          <Card className="-order-1 xl:sticky xl:top-5 xl:order-none">
            <CardHeader>
              <CardTitle>
                <h2>{copy.yourPosition}</h2>
              </CardTitle>
              <CardDescription>
                <span className="font-heading text-3xl font-semibold text-foreground tabular-nums">#{around.entry.rank.toLocaleString(locale)}</span>
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Rows entries={around.around} meId={me.id} valueLabel={copy.values[board]} />
            </CardContent>
          </Card>
        )}
      </div>
    </main>
  )
}

function Rows({ entries, meId, valueLabel }: { entries: LeaderboardEntryDto[]; meId: string; valueLabel: string }) {
  const { t, locale } = useLocale()

  return (
    <ol className="grid gap-1.5">
      {entries.map((entry) => {
        const isMe = entry.user.id === meId
        return (
          <li
            key={`${entry.rank}-${entry.user.id}`}
            aria-current={isMe ? 'true' : undefined}
            className={cn('flex items-center gap-3 rounded-lg px-3 py-2.5', 'ring-1 ring-inset', isMe ? 'bg-primary/10 ring-primary/40' : 'ring-border')}
          >
            <span className="w-12 shrink-0 font-mono text-sm text-muted-foreground tabular-nums">{entry.rank.toLocaleString(locale)}</span>
            <UserAvatar user={entry.user} className="size-10" />
            <span className="grid min-w-0 flex-1">
              <span className="flex min-w-0 items-center gap-2">
                <span className="truncate text-sm font-medium">{entry.user.username}</span>
                {isMe && <Badge variant="outline">{t.leaderboards.you}</Badge>}
              </span>
              <span className="truncate text-xs text-muted-foreground">{t.dashboard.tiers[entry.user.tier]}</span>
            </span>
            <span className="shrink-0 font-mono text-sm font-medium tabular-nums">
              {entry.value}
              <span className="sr-only"> {valueLabel}</span>
            </span>
          </li>
        )
      })}
    </ol>
  )
}
