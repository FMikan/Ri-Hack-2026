import { Flag, Ghost, WifiOff } from 'lucide-react'
import { createElement, useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { usePoll } from '@/hooks/use-poll'
import { useTicker } from '@/hooks/use-ticker'
import { api, ApiError } from '@/lib/api-client'
import { boardClass } from '@/lib/cosmetics'
import { relativeTime } from '@/lib/format'
import { ratingText } from '@/lib/game'
import type { Messages } from '@/lib/i18n/en'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import { cn } from '@/lib/utils'
import type { AnswerPayload, PlayerDto } from '@/types/api'
import { AnswerChip } from './AnswerChip'
import { Explanation } from './Explanation'
import { PlayerAvatar } from './PlayerAvatar'
import { QuestionBody } from './QuestionBody'
import { ResultView } from './ResultView'
import { msToDeadline, type Command, type Snapshot } from './snapshot'
import { VetoView } from './VetoView'

const isOver = ({ match }: Snapshot) => match.status === 'finished' || match.status === 'aborted'

const playerName = (player: PlayerDto | undefined, t: Messages) =>
  player?.kind === 'bot' && player.persona ? t.bots[player.persona] : (player?.user?.username ?? '')

// Full-screen duel: veto (ranked and friend), lobby, ten questions with interstitials, then the result.
// Keyed by id, so a rematch starts with a fresh poll and no leftovers from the last duel.
export function MatchPage() {
  const { id = '' } = useParams()
  return <MatchScreen key={id} id={id} />
}

function MatchScreen({ id }: { id: string }) {
  const { t } = useLocale()
  const { session } = useAuth()
  // The duel board I equipped in the shop (it only changes my own screen).
  const board = boardClass(session.status === 'authenticated' ? session.user : null)
  const load = useCallback(
    (signal: AbortSignal) => api.match(id, signal).then((match) => ({ match, at: performance.now() })),
    [id],
  )
  const { data, error, replace, refresh } = usePoll(load, 1000, isOver)
  const [commandError, setCommandError] = useState<string>()

  const command: Command = useCallback(
    async (run) => {
      setCommandError(undefined)
      try {
        replace({ match: await run(), at: performance.now() })
        return true
      } catch (caught) {
        // Late or duplicate commands (the phase already moved on): take the server's state and show no error.
        if (caught instanceof ApiError && caught.code?.startsWith('urn:app:match/')) {
          refresh()
          return true
        }
        setCommandError(caught instanceof ApiError && caught.status === 0 ? t.common.serverUnreachable : String((caught as Error).message))
        return false
      }
    },
    [replace, refresh, t],
  )

  return (
    <div className={cn('min-h-svh', board)}>
      <main className="mx-auto grid min-h-svh w-full max-w-3xl content-start gap-4 px-4 py-4 sm:gap-5 sm:px-6 sm:py-8">
        <title>{`${t.match.documentTitle} · StackOff`}</title>
        <h1 className="sr-only">{t.match.documentTitle}</h1>
        {data ? (
          <MatchView snapshot={data} command={command} />
        ) : error ? (
          <Card role="alert">
            <CardContent className="grid justify-items-start gap-3">
              <p>{t.match.loadFailed}</p>
              <p className="text-sm text-muted-foreground">{error.message}</p>
              <Button asChild variant="outline">
                <Link to="/">{t.match.result.dashboard}</Link>
              </Button>
            </CardContent>
          </Card>
        ) : (
          <div aria-busy="true" className="grid gap-4">
            <p role="status" className="sr-only">
              {t.common.loading}
            </p>
            <Skeleton className="h-20 rounded-xl" />
            <Skeleton className="h-80 rounded-xl" />
          </div>
        )}
        {data && error && (
          <p role="status" className="justify-self-center rounded-full bg-secondary px-3 py-1 text-xs text-muted-foreground">
            {t.match.reconnecting}
          </p>
        )}
        {commandError && (
          <p role="alert" className="text-sm text-destructive">
            {commandError}
          </p>
        )}
      </main>
    </div>
  )
}

function MatchView({ snapshot, command }: { snapshot: Snapshot; command: Command }) {
  const { t } = useLocale()
  const { match } = snapshot
  const me = match.players.find((player) => player.seat === match.mySeat)
  const opponent = match.players.find((player) => player.seat !== match.mySeat)
  const opponentName = playerName(opponent, t)
  const solo = !opponent // practice and calibration

  if (match.result) return <ResultView match={match} me={me} opponent={opponent} opponentName={opponentName} />
  if (match.veto && match.phase.startsWith('veto_'))
    return <VetoView snapshot={snapshot} command={command} me={me} opponent={opponent} opponentName={opponentName} />
  if (!match.question || (match.phase !== 'question' && match.phase !== 'interstitial'))
    return <Lobby snapshot={snapshot} me={me} opponent={opponent} opponentName={opponentName} />

  return (
    <>
      <div className="flex items-center gap-3">
        <p className="flex min-w-0 items-center gap-2 text-sm text-muted-foreground">
          {createElement(topicIcon(match.topic?.slug ?? ''), { 'aria-hidden': true, className: 'size-4 shrink-0 text-primary' })}
          <span className="truncate">{match.topic?.name}</span>
          <span aria-hidden="true">·</span>
          <span className="shrink-0 font-mono text-xs">{t.match.question(match.question.index + 1, match.question.total)}</span>
        </p>
        <ResignButton solo={solo} onResign={() => command(() => api.resign(match.id))} />
      </div>
      <Scoreboard
        me={me}
        opponent={opponent}
        opponentName={opponentName}
        opponentAnswered={match.phase === 'question' && !!opponent?.hasAnswered}
      />
      <QuestionStage key={match.question.index} snapshot={snapshot} command={command} opponentName={opponentName} />
    </>
  )
}

interface LobbyProps {
  snapshot: Snapshot
  me?: PlayerDto
  opponent?: PlayerDto
  opponentName: string
}

// 5 seconds to look at who you play and on what, then the first question. A ghost shows when it was recorded.
function Lobby({ snapshot, me, opponent, opponentName }: LobbyProps) {
  const { t, locale } = useLocale()
  const now = useTicker(true)
  const seconds = Math.max(0, Math.ceil(msToDeadline(snapshot, now) / 1000))
  const side = (player: PlayerDto | undefined, name: string) => (
    <div className="grid w-36 justify-items-center gap-2 sm:w-44">
      <span className="relative">
        <PlayerAvatar player={player} className="size-16 sm:size-20" />
        {player?.kind === 'ghost' && (
          <Ghost aria-hidden="true" className="absolute -right-1 -bottom-1 size-7 rounded-full bg-card p-1 text-primary" />
        )}
      </span>
      <p className="font-heading text-lg leading-tight font-semibold text-balance">{name}</p>
      <p className="font-mono text-xs text-muted-foreground">
        {player?.user && `${t.dashboard.tiers[player.user.tier]} · `}
        {player && ratingText(player.displayRating, player.user?.isProvisional)}
      </p>
      {player?.kind === 'ghost' && player.ghostRecordedAt && (
        <p className="text-xs text-pretty text-muted-foreground">{t.match.ghostRecorded(relativeTime(player.ghostRecordedAt, locale))}</p>
      )}
    </div>
  )

  return (
    <section className="grid min-h-[70svh] place-content-center justify-items-center gap-8 text-center">
      <p className="text-sm text-muted-foreground">{snapshot.match.topic?.name}</p>
      <div className="flex items-start gap-4 sm:gap-8">
        {side(me, t.match.you)}
        <span className="pt-6 font-mono text-sm text-muted-foreground sm:pt-8">vs</span>
        {side(opponent, opponentName)}
      </div>
      {Number.isFinite(seconds) && (
        <div className="grid gap-1">
          <p className="text-sm text-muted-foreground">{t.match.startsIn}</p>
          <p className="font-heading text-7xl font-semibold tabular-nums">{seconds}</p>
        </div>
      )}
    </section>
  )
}

interface ScoreboardProps {
  me?: PlayerDto
  opponent?: PlayerDto
  opponentName: string
  opponentAnswered: boolean
}

function Scoreboard({ me, opponent, opponentName, opponentAnswered }: ScoreboardProps) {
  const { t } = useLocale()
  const side = (player: PlayerDto | undefined, name: string, answered: boolean, end?: boolean) => (
    <div className={cn('flex min-w-0 items-center gap-3', end && 'flex-row-reverse text-right')}>
      <PlayerAvatar player={player} className="hidden size-10 sm:block" />
      <div className="grid min-w-0">
        <p className="truncate text-sm font-medium">{name}</p>
        {player?.kind === 'human' && !player.connected ? (
          <p className="flex items-center gap-1 text-xs text-warning">
            <WifiOff aria-hidden="true" className="size-3" />
            {t.match.disconnected}
          </p>
        ) : (
          <p className={cn('font-mono text-xs', answered ? 'text-success' : 'text-muted-foreground')}>
            {answered ? t.match.answered : opponent && player && ratingText(player.displayRating, player.user?.isProvisional)}
          </p>
        )}
      </div>
      <p className={cn('font-heading text-2xl font-semibold tabular-nums sm:text-3xl', end ? 'mr-auto' : 'ml-auto')}>
        {player?.score ?? 0}
      </p>
    </div>
  )

  if (!opponent)
    return <div className="grid rounded-xl bg-card p-3 ring-1 ring-foreground/10 sm:p-4">{side(me, t.match.you, false)}</div>

  return (
    <div className="grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-3 rounded-xl bg-card p-3 ring-1 ring-foreground/10 sm:p-4">
      {side(me, t.match.you, false)}
      <span className="font-mono text-xs text-muted-foreground">vs</span>
      {side(opponent, opponentName, opponentAnswered, true)}
      <p aria-live="polite" className="sr-only">
        {opponentAnswered && `${opponentName}: ${t.match.answered}`}
      </p>
    </div>
  )
}

// Asks for a second tap within 3 seconds before resigning (ending, in practice).
function ResignButton({ solo, onResign }: { solo: boolean; onResign: () => void }) {
  const { t } = useLocale()
  const [confirming, setConfirming] = useState(false)
  useEffect(() => {
    if (!confirming) return
    const id = window.setTimeout(() => setConfirming(false), 3000)
    return () => window.clearTimeout(id)
  }, [confirming])

  return (
    <Button
      variant={confirming ? 'destructive' : 'ghost'}
      size="sm"
      className="ml-auto shrink-0"
      onClick={() => (confirming ? onResign() : setConfirming(true))}
    >
      <Flag aria-hidden="true" />
      {solo ? (confirming ? t.match.endConfirm : t.match.end) : confirming ? t.match.resignConfirm : t.match.resign}
    </Button>
  )
}

interface QuestionStageProps {
  snapshot: Snapshot
  command: Command
  opponentName: string
}

// One question, keyed by its index so local picks and the timer start fresh for every question.
function QuestionStage({ snapshot, command, opponentName }: QuestionStageProps) {
  const { t } = useLocale()
  const { match } = snapshot
  const turn = match.question!
  const interstitial = match.phase === 'interstitial' ? match.interstitial : undefined
  // The answer time runs from the first render; after a reload it continues from when the question was delivered.
  const [startedAt] = useState(
    () => performance.now() - (turn.deliveredAt ? Math.max(0, Date.parse(match.serverTime) - Date.parse(turn.deliveredAt)) : 0),
  )
  const [pending, setPending] = useState<AnswerPayload>()
  const [moving, setMoving] = useState(false)
  const now = useTicker(!interstitial && match.timed)
  const limit = turn.question.timeLimitMs
  const left = interstitial
    ? 0
    : match.timed
      ? Math.max(0, Math.min(limit - (now - startedAt), msToDeadline(snapshot, now) - 500))
      : Infinity
  // Untimed practice: no deadline in the interstitial, it waits until the player moves on.
  const waitsForNext = !!interstitial && !match.phaseDeadline

  async function next() {
    setMoving(true)
    if (!(await command(() => api.next(match.id)))) setMoving(false)
  }
  const answered = !!turn.myAnswer || pending !== undefined
  const mine = interstitial ? interstitial.mine.payload : (turn.myAnswer?.payload ?? pending)

  async function answer(payload: AnswerPayload) {
    if (answered || left <= 0) return
    setPending(payload)
    const elapsed = Math.round(performance.now() - startedAt)
    const sent = await command(() =>
      api.answer(match.id, { questionIndex: turn.index, payload, clientElapsedMs: elapsed }),
    )
    if (!sent) setPending(undefined)
  }

  return (
    <section className="grid gap-5">
      {match.timed && <TimerBar left={left} total={limit} hidden={!!interstitial} />}
      <Card>
        <CardContent>
          <QuestionBody
            content={turn.question.content}
            mine={mine}
            correct={interstitial?.key.answer}
            locked={answered || left <= 0 || !!interstitial}
            onAnswer={answer}
          />
        </CardContent>
      </Card>
      {interstitial ? (
        <div className="grid gap-3 rounded-xl bg-card p-4 ring-1 ring-foreground/10">
          <div className="flex flex-wrap gap-2">
            <AnswerChip who={t.match.you} result={interstitial.mine} />
            {interstitial.opponent && <AnswerChip who={opponentName} result={interstitial.opponent} />}
          </div>
          {waitsForNext && <Explanation questionId={turn.question.id} />}
          <p className="text-sm text-pretty">
            <span className="font-medium text-primary">{t.match.remember}:</span> {interstitial.key.remember}
          </p>
          {waitsForNext && (
            <Button size="lg" className="justify-self-end" aria-disabled={moving} onClick={() => !moving && next()}>
              {t.match.next}
            </Button>
          )}
        </div>
      ) : (
        <p aria-live="polite" className="min-h-5 text-center text-sm text-muted-foreground">
          {answered && opponentName ? t.match.waiting(opponentName) : left <= 0 ? t.match.timeUp : ''}
        </p>
      )}
    </section>
  )
}

function TimerBar({ left, total, hidden }: { left: number; total: number; hidden: boolean }) {
  const { t } = useLocale()
  const seconds = Math.ceil(left / 1000)
  const urgent = left <= 5000

  return (
    <div className={cn('flex items-center gap-3', hidden && 'invisible')}>
      <div aria-hidden="true" className="h-2 flex-1 overflow-hidden rounded-full bg-secondary">
        <div
          className={cn('h-full rounded-full transition-[width] duration-100 ease-linear', urgent ? 'bg-destructive' : 'bg-primary')}
          style={{ width: `${(left / total) * 100}%` }}
        />
      </div>
      <span className={cn('w-8 text-right font-mono text-sm tabular-nums', urgent && 'text-destructive')}>
        <span aria-hidden="true">{seconds}</span>
        <span className="sr-only">{t.match.secondsLeft(seconds)}</span>
      </span>
    </div>
  )
}
