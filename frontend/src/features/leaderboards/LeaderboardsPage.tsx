import { ChevronDown } from 'lucide-react'
import { createElement, useCallback, useState } from 'react'
import { Link } from 'react-router'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { nameClass } from '@/lib/cosmetics'
import { relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import { cn } from '@/lib/utils'
import type { Board, LeaderboardDto, LeaderboardEntryDto } from '@/types/api'

const BOARDS: Board[] = ['global', 'season', 'weekly', 'friends', 'topic']
const MEDALS = ['bg-medal-gold', 'bg-medal-silver', 'bg-medal-bronze']

// The five leaderboards from the game idea (§14): top 100 plus my own row with the players around it.
export function LeaderboardsPage() {
  const { t, locale } = useLocale()
  const copy = t.leaderboards
  const { session } = useAuth()
  const topics = useAsync(api.listTopics)
  const season = useAsync(api.season)
  const [board, setBoard] = useState<Board>('global')
  const [topicSlug, setTopicSlug] = useState<string>()
  const pickedSlug = topicSlug ?? topics.data?.[0]?.slug
  const slug = board === 'topic' ? pickedSlug : undefined
  const load = useCallback(
    // The topic board waits for the topic list, so it never asks without ?topic=.
    (signal: AbortSignal) => (board === 'topic' && !slug ? new Promise<never>(() => {}) : api.leaderboard(board, slug, signal)),
    [board, slug],
  )
  const data = useAsync(load)
  if (session.status !== 'authenticated') return null
  const me = session.user
  // Kept while the next board loads, so labels come from the board the data belongs to.
  const shown = data.data && data.data.board === board ? data.data : undefined

  return (
    <main className="mx-auto grid w-full max-w-7xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${copy.documentTitle} · StackOff`}</title>
      <header className="grid gap-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{copy.title}</h1>
        <p className="text-sm text-pretty text-muted-foreground">{copy.subtitle}</p>
      </header>

      <div className="grid gap-3">
        {/* One pill: the board tabs, and on "By topic" the pill widens to the right with the topic picker joined to
            that tab (same tint, square seam, thin divider). Neither shows on phones: five tabs would wrap there. */}
        <div className="flex max-w-full items-stretch justify-self-start rounded-lg bg-secondary">
          <SegmentedControl
            label={copy.board}
            name="board"
            value={board}
            onChange={setBoard}
            options={BOARDS.map((value) => ({
              value,
              label: copy.boards[value],
              className: value === 'topic' ? 'max-sm:hidden has-checked:rounded-r-none' : undefined,
            }))}
            className={cn('bg-transparent', board === 'topic' && 'sm:pr-0')}
          />
          {topics.data && pickedSlug && (
            <div
              inert={board !== 'topic'}
              className={cn(
                'grid transition-[grid-template-columns,opacity] duration-300 ease-[cubic-bezier(0.2,0,0,1)] max-sm:hidden motion-reduce:transition-none',
                board === 'topic' ? 'grid-cols-[1fr] opacity-100' : 'grid-cols-[0fr] opacity-0',
              )}
            >
              <div className="min-w-0 overflow-hidden py-1 pr-1">
                <DropdownMenu>
                  <DropdownMenuTrigger
                    className={cn(
                      'flex h-full items-center gap-2 rounded-r-md border-l border-primary/25 bg-primary/15 px-3.5 text-sm font-medium whitespace-nowrap text-primary transition-colors outline-none hover:bg-primary/20',
                      'focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring',
                    )}
                  >
                    {createElement(topicIcon(pickedSlug), { 'aria-hidden': true, className: 'size-4' })}
                    <span className="sr-only">{copy.topic}: </span>
                    {topics.data.find((topic) => topic.slug === pickedSlug)?.name}
                    <ChevronDown aria-hidden="true" className="size-4 opacity-70" />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end" className="w-auto min-w-(--radix-dropdown-menu-trigger-width)">
                    <DropdownMenuLabel className="font-normal text-muted-foreground">{copy.topic}</DropdownMenuLabel>
                    <DropdownMenuRadioGroup value={pickedSlug} onValueChange={setTopicSlug}>
                      {topics.data.map((topic) => (
                        <DropdownMenuRadioItem key={topic.id} value={topic.slug}>
                          {createElement(topicIcon(topic.slug), { 'aria-hidden': true, className: 'text-primary' })}
                          {topic.name}
                        </DropdownMenuRadioItem>
                      ))}
                    </DropdownMenuRadioGroup>
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
            </div>
          )}
        </div>
        <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-muted-foreground">
          {copy.hints[board]}
          {board === 'season' && season.data && <span>· {copy.season(season.data.number)}</span>}
          {shown?.resetsAt && <span className="font-mono text-xs">· {copy.resets(relativeTime(shown.resetsAt, locale))}</span>}
          {board === 'season' && season.data?.doubleLearningSp && <Badge variant="success">{copy.doubleSp}</Badge>}
        </p>
      </div>

      {shown ? (
        <BoardView data={shown} meId={me.id} />
      ) : data.error && !data.loading ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{copy.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{data.error.message}</p>
            <Button variant="outline" onClick={data.reload}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div aria-busy="true" className="grid gap-4">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          <div className="grid grid-cols-3 gap-3 sm:gap-4 lg:gap-5">
            {Array.from({ length: 3 }, (_, index) => (
              <Skeleton key={index} className="h-48 rounded-xl" />
            ))}
          </div>
          <Skeleton className="h-80 rounded-xl" />
        </div>
      )}
    </main>
  )
}

function BoardView({ data, meId }: { data: LeaderboardDto; meId: string }) {
  const { t, locale } = useLocale()
  const copy = t.leaderboards
  const valueLabel = copy.values[data.board]
  const podium = data.entries.slice(0, 3)
  const rest = data.entries.slice(3)
  // My own rows only when I am not on the list already (friends always include me, highlighted in place).
  const around = data.me && !data.entries.some((entry) => entry.user.id === meId) ? data.me : undefined

  if (!data.entries.length)
    return (
      <Card>
        <CardContent className="grid gap-1">
          <p className="font-medium">{copy.empty[data.board]}</p>
          {data.me && <p className="text-sm text-muted-foreground">{copy.yourRank(data.me.entry.rank)}</p>}
        </CardContent>
      </Card>
    )

  return (
    <>
      <ol className="grid grid-cols-3 gap-3 sm:gap-4 lg:gap-5">
        {podium.map((entry, index) => (
          <li key={entry.user.id} className="min-w-0">
            <Link
              to={entry.user.id === meId ? '/profile' : `/u/${entry.user.username}`}
              className="block h-full rounded-xl focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              <Card
                className={cn(
                  'h-full transition-colors hover:bg-secondary max-sm:[--card-spacing:--spacing(3)]',
                  entry.user.id === meId && 'ring-2 ring-primary',
                )}
              >
                <CardContent className="grid justify-items-center gap-2 text-center">
                  <span className={cn('grid size-8 place-items-center rounded-full font-mono text-sm font-semibold text-background', MEDALS[index])}>
                    {entry.rank}
                  </span>
                  <UserAvatar user={entry.user} className="size-12 sm:size-14" />
                  <p className={cn('line-clamp-2 w-full text-xs font-medium [overflow-wrap:anywhere] sm:text-sm', nameClass(entry.user))}>
                    {entry.user.username}
                  </p>
                  <p className="hidden text-xs text-muted-foreground sm:block">{entry.user.title ?? t.dashboard.tiers[entry.user.tier]}</p>
                  <p className="font-heading text-xl font-semibold tabular-nums sm:text-3xl">{entry.value}</p>
                  <p className="text-xs text-muted-foreground">{valueLabel}</p>
                </CardContent>
              </Card>
            </Link>
          </li>
        ))}
      </ol>

      <div className="grid items-start gap-3 sm:gap-4 lg:gap-5 xl:grid-cols-3">
        {rest.length > 0 && (
          <Card className={around ? 'xl:col-span-2' : 'xl:col-span-3'}>
            <CardContent>
              <Rows entries={rest} meId={meId} valueLabel={valueLabel} />
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
              <Rows entries={around.around} meId={meId} valueLabel={valueLabel} />
            </CardContent>
          </Card>
        )}
      </div>
    </>
  )
}

function Rows({ entries, meId, valueLabel }: { entries: LeaderboardEntryDto[]; meId: string; valueLabel: string }) {
  const { t, locale } = useLocale()

  return (
    <ol className="grid gap-1.5">
      {entries.map((entry) => {
        const isMe = entry.user.id === meId
        return (
          <li key={`${entry.rank}-${entry.user.id}`} aria-current={isMe ? 'true' : undefined}>
            <Link
              to={isMe ? '/profile' : `/u/${entry.user.username}`}
              className={cn(
                'flex items-center gap-3 rounded-lg px-3 py-2.5 ring-1 transition-colors ring-inset hover:bg-secondary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
                isMe ? 'bg-primary/10 ring-primary/40' : 'ring-border',
              )}
            >
              <span className="w-12 shrink-0 font-mono text-sm text-muted-foreground tabular-nums">{entry.rank.toLocaleString(locale)}</span>
              <UserAvatar user={entry.user} className="size-10" />
              <span className="grid min-w-0 flex-1">
                <span className="flex min-w-0 items-center gap-2">
                  <span className={cn('truncate text-sm font-medium', nameClass(entry.user))}>{entry.user.username}</span>
                  {isMe && <Badge variant="outline">{t.leaderboards.you}</Badge>}
                </span>
                <span className="truncate text-xs text-muted-foreground">{entry.user.title ?? t.dashboard.tiers[entry.user.tier]}</span>
              </span>
              <span className="shrink-0 font-mono text-sm font-medium tabular-nums">
                {entry.value}
                <span className="sr-only"> {valueLabel}</span>
              </span>
            </Link>
          </li>
        )
      })}
    </ol>
  )
}
