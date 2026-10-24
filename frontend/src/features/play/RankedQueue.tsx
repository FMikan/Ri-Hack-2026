import { Ghost, LoaderCircle } from 'lucide-react'
import { createElement, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { DuelBadge } from '@/components/shared/RankedDuelIcon'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DialogClose } from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
import { useOpenMatch } from '@/features/match/use-start-match'
import { useAsync } from '@/hooks/use-async'
import { usePoll } from '@/hooks/use-poll'
import { useTicker } from '@/hooks/use-ticker'
import { api, ApiError } from '@/lib/api-client'
import { relativeTime } from '@/lib/format'
import { MIN_TOPICS, ratingText } from '@/lib/game'
import type { Messages } from '@/lib/i18n/en'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import type { MeDto, QueueStateDto, TopicDto } from '@/types/api'
import { dialogFooter } from './dialog-footer'
import { DialogHeading, ResumeBanner } from './dialog-parts'

const loadQueue = (signal: AbortSignal) => Promise.all([api.me(signal), api.listTopics(signal)])

// A poll response plus when it arrived: clocks run on the server's time (serverTime), never the device clock.
interface Snapshot {
  state: QueueStateDto
  at: number
}
const loadState = (signal: AbortSignal) => api.queue(signal).then((state) => ({ state, at: performance.now() }))
const isMatched = ({ state }: Snapshot) => state.status === 'matched'
const sinceServer = ({ state, at }: Snapshot, iso: string, now: number) => Date.parse(state.serverTime) - Date.parse(iso) + (now - at)

const errorText = (caught: unknown, t: Messages) =>
  caught instanceof ApiError && caught.status === 0 ? t.common.serverUnreachable : String((caught as Error).message)

// Ranked duel: opening the dialog joins the queue (POST /api/queue) and polls it once a second. Closing the dialog
// leaves it (PlayDialog). A proposal needs both players to accept within 10 s; after 30 s a ghost or a bot is offered.
export function RankedQueue({ onPlayBot }: { onPlayBot: () => void }) {
  const { t } = useLocale()
  const data = useAsync(loadQueue)

  return (
    <>
      <DialogHeading title={t.queue.title} description={t.queue.searching} />
      {data.data ? (
        <Join me={data.data[0]} topics={data.data[1]} onPlayBot={onPlayBot} />
      ) : data.error ? (
        <div role="alert" className="grid justify-items-start gap-3">
          <p>{t.play.loadFailed}</p>
          <p className="text-sm text-muted-foreground">{data.error.message}</p>
          <Button variant="outline" onClick={data.reload}>
            {t.common.retry}
          </Button>
        </div>
      ) : (
        <Skeleton aria-busy="true" className="h-56 rounded-lg" />
      )}
    </>
  )
}

interface JoinProps {
  me: MeDto
  topics: TopicDto[]
  onPlayBot: () => void
}

function Join({ me, topics, onPlayBot }: JoinProps) {
  const { t } = useLocale()
  const [joined, setJoined] = useState(me.inQueue)
  const [locked, setLocked] = useState(me.activeTopicIds.length < MIN_TOPICS)
  const [resumeId, setResumeId] = useState(me.activeMatchId)
  const [error, setError] = useState<string>()

  useEffect(() => {
    if (joined || locked || resumeId) return
    let live = true
    api.joinQueue().then(
      () => live && setJoined(true),
      async (caught) => {
        if (!live) return
        const code = caught instanceof ApiError ? caught.code : undefined
        if (code === 'urn:app:queue/already-in') return setJoined(true)
        if (code === 'urn:app:topics/count') return setLocked(true)
        if (code === 'urn:app:match/already-in-match') {
          const fresh = await api.me().catch(() => undefined)
          if (fresh?.activeMatchId) return setResumeId(fresh.activeMatchId)
        }
        setError(errorText(caught, t))
      },
    )
    return () => {
      live = false
    }
  }, [joined, locked, resumeId, t])

  if (resumeId) return <ResumeBanner matchId={resumeId} />
  if (locked)
    return (
      <div className="grid justify-items-start gap-3">
        <p>{t.queue.locked}</p>
        <Button asChild variant="outline">
          <Link to="/#topics">{t.queue.pickTopics}</Link>
        </Button>
      </div>
    )
  if (error)
    return (
      <div role="alert" className="grid justify-items-start gap-3">
        <p className="text-sm text-destructive">{error}</p>
        <Button variant="outline" onClick={() => setError(undefined)}>
          {t.common.retry}
        </Button>
      </div>
    )
  if (!joined) return <Skeleton aria-busy="true" className="h-56 rounded-lg" />
  return <Searching me={me} topics={topics} onPlayBot={onPlayBot} onRejoin={() => setJoined(false)} />
}

interface SearchingProps {
  me: MeDto
  topics: TopicDto[]
  onPlayBot: () => void
  onRejoin: () => void
}

function Searching({ me, topics, onPlayBot, onRejoin }: SearchingProps) {
  const { t, locale } = useLocale()
  const copy = t.queue
  const openMatch = useOpenMatch()
  const { data, error, replace, refresh } = usePoll(loadState, 1000, isMatched)
  const [keepWaiting, setKeepWaiting] = useState(false)
  const [busy, setBusy] = useState(false)
  const [commandError, setCommandError] = useState<string>()
  const now = useTicker(!!data && data.state.status !== 'matched', 250)
  const matchId = data?.state.status === 'matched' ? data.state.matchId : undefined

  useEffect(() => {
    if (matchId) openMatch(matchId)
  }, [matchId, openMatch])

  async function run(command: () => Promise<QueueStateDto | void>) {
    setBusy(true)
    setCommandError(undefined)
    try {
      const state = await command()
      if (state) replace({ state, at: performance.now() })
    } catch (caught) {
      // The offer ran out or the other player declined: the next poll shows where we are.
      if (caught instanceof ApiError && (caught.code === 'urn:app:proposal/expired' || caught.code === 'urn:app:queue/no-ghost-offer')) refresh()
      else setCommandError(errorText(caught, t))
    } finally {
      setBusy(false)
    }
  }

  async function playGhost() {
    setBusy(true)
    setCommandError(undefined)
    try {
      openMatch((await api.playGhost()).id)
    } catch (caught) {
      if (caught instanceof ApiError && caught.code === 'urn:app:queue/no-ghost-offer') refresh()
      else setCommandError(errorText(caught, t))
      setBusy(false)
    }
  }

  if (!data)
    return error ? (
      <p role="alert" className="text-sm text-destructive">
        {errorText(error, t)}
      </p>
    ) : (
      <Skeleton aria-busy="true" className="h-56 rounded-lg" />
    )

  const { state } = data
  const footer = (
    <div className={dialogFooter}>
      {commandError && (
        <p role="alert" className="mr-auto text-sm text-destructive">
          {commandError}
        </p>
      )}
      {keepWaiting && state.status === 'waiting' && (
        <Button variant="ghost" className="mr-auto" onClick={onPlayBot}>
          {copy.playBot}
        </Button>
      )}
      <DialogClose asChild>
        <Button variant="outline" size="lg" className="w-full sm:w-auto">
          {copy.cancel}
        </Button>
      </DialogClose>
    </div>
  )

  if (state.status === 'matched')
    return (
      <p role="status" className="flex items-center justify-center gap-2 py-10 text-muted-foreground">
        <LoaderCircle aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
        {copy.opening}
      </p>
    )

  if (state.status === 'idle')
    return (
      <div className="grid justify-items-start gap-3">
        <p>{copy.left}</p>
        <Button onClick={onRejoin}>{copy.searchAgain}</Button>
      </div>
    )

  if (state.status === 'proposed' && state.proposal) {
    const { proposal } = state
    const left = Math.max(0, Math.ceil((Date.parse(proposal.expiresAt) - Date.parse(state.serverTime) - (now - data.at)) / 1000))
    return (
      <div className="grid gap-6">
        <div aria-live="polite" className="grid justify-items-center gap-3 pt-2 text-center">
          <p className="font-heading text-2xl font-semibold tracking-tight">{copy.found}</p>
          <UserAvatar user={proposal.opponent} className="size-20" />
          <div className="grid gap-0.5">
            <p className="font-medium">{proposal.opponent.username}</p>
            <p className="text-sm text-muted-foreground">
              {t.dashboard.tiers[proposal.opponent.tier]} · {ratingText(proposal.opponent.rating, proposal.opponent.isProvisional)}
            </p>
          </div>
          {proposal.iAccepted ? (
            <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
              <LoaderCircle aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
              {copy.waitingAccept(proposal.opponent.username)}
            </p>
          ) : (
            <>
              <p role="timer" className="text-sm text-muted-foreground tabular-nums">
                {copy.acceptWithin(left)}
              </p>
              <Button size="lg" aria-disabled={busy} onClick={() => !busy && run(() => api.acceptProposal(proposal.id))}>
                {copy.accept}
              </Button>
            </>
          )}
        </div>
        {footer}
      </div>
    )
  }

  const waited = state.enteredAt ? Math.max(0, sinceServer(data, state.enteredAt, now)) : 0
  const range = state.rangeNow ?? 0
  const mine = topics.filter((topic) => state.topicIds.includes(topic.id))
  const ghost = state.ghostOffer

  return (
    <div className="grid gap-6">
      <div className="grid justify-items-center gap-3 pt-2 text-center">
        <span className="relative grid size-24 place-items-center">
          <span aria-hidden="true" className="absolute inset-2 animate-ping rounded-full bg-primary/25 motion-reduce:hidden" />
          <DuelBadge className="relative size-20" />
        </span>
        <p className="text-sm text-muted-foreground">{copy.elapsed}</p>
        <p role="timer" className="font-heading text-5xl font-semibold tabular-nums">
          {clock(waited)}
        </p>
        {range > 0 && (
          <Badge variant="secondary" className="font-mono">
            {copy.range(me.rating - range, me.rating + range)}
          </Badge>
        )}
      </div>

      <section className="grid justify-items-center gap-2.5">
        <h3 className="text-sm font-medium text-muted-foreground">{copy.topics}</h3>
        <ul className="flex flex-wrap justify-center gap-2">
          {mine.map((topic) => (
            <li key={topic.id}>
              <Badge variant="outline" className="h-8 gap-1.5 px-3 text-sm">
                {createElement(topicIcon(topic.slug), { 'aria-hidden': true, className: 'text-primary' })}
                {topic.name}
              </Badge>
            </li>
          ))}
        </ul>
      </section>

      {ghost ? (
        <div aria-live="polite" className="grid gap-3 rounded-lg bg-secondary p-4 ring-1 ring-border ring-inset sm:flex sm:items-center">
          <span className="relative shrink-0 self-start">
            <UserAvatar user={ghost.opponent} className="size-12" />
            <Ghost aria-hidden="true" className="absolute -right-1 -bottom-1 size-5 rounded-full bg-card p-0.5 text-primary" />
          </span>
          <div className="grid flex-1 gap-1">
            <p className="font-medium">{copy.ghost.title}</p>
            <p className="text-sm text-pretty text-muted-foreground">
              {copy.ghost.body(ghost.opponent.username, ghost.topic.name, relativeTime(ghost.recordedAt, locale))}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button aria-disabled={busy} onClick={() => !busy && playGhost()}>
              {copy.ghost.play}
            </Button>
            <Button variant="outline" aria-disabled={busy} onClick={() => !busy && run(api.declineGhost)}>
              {copy.keepWaiting}
            </Button>
          </div>
        </div>
      ) : (
        state.botOffer &&
        !keepWaiting && (
          <div aria-live="polite" className="grid gap-3 rounded-lg bg-secondary p-4 ring-1 ring-border ring-inset sm:flex sm:items-center">
            <p className="flex-1 text-sm text-pretty">{copy.noOpponent}</p>
            <div className="flex flex-wrap gap-2">
              <Button onClick={onPlayBot}>{copy.playBot}</Button>
              <Button variant="outline" onClick={() => setKeepWaiting(true)}>
                {copy.keepWaiting}
              </Button>
            </div>
          </div>
        )
      )}

      {footer}
    </div>
  )
}

const clock = (ms: number) => {
  const seconds = Math.floor(ms / 1000)
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`
}
