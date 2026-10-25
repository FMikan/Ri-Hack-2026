import { Check, Flag, Ghost, Timer, WifiOff } from 'lucide-react'
import { createElement, useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { seat, SeatFrame } from '@/components/shared/SeatFrame'
import { TierIcon } from '@/components/shared/TierIcon'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardFooter } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { usePoll } from '@/hooks/use-poll'
import { useTicker } from '@/hooks/use-ticker'
import { api, ApiError } from '@/lib/api-client'
import { boardClass, nameClass } from '@/lib/cosmetics'
import { relativeTime } from '@/lib/format'
import { LOBBY_SECONDS, QUESTIONS_PER_MATCH, ratingText } from '@/lib/game'
import type { Messages } from '@/lib/i18n/en'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import { cn, skyOutline } from '@/lib/utils'
import type { AnswerPayload, PlayerDto } from '@/types/api'
import { RoundResult } from './AnswerChip'
import { Explanation, Remember } from './Explanation'
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
          <span className="shrink-0 font-mono text-xs text-foreground tabular-nums">
            {t.match.question(match.question.index + 1, match.question.total)}
          </span>
        </p>
        <ResignButton solo={solo} onResign={() => command(() => api.resign(match.id))} />
      </div>
      <Scoreboard
        me={me}
        opponent={opponent}
        opponentName={opponentName}
        meAnswered={match.phase === 'question' && !!match.question.myAnswer}
        opponentAnswered={match.phase === 'question' && !!opponent?.hasAnswered}
      />
      <QuestionStage
        key={match.question.index}
        snapshot={snapshot}
        command={command}
        me={me}
        opponent={opponent}
        opponentName={opponentName}
      />
    </>
  )
}

interface LobbyProps {
  snapshot: Snapshot
  me?: PlayerDto
  opponent?: PlayerDto
  opponentName: string
}

// 5 seconds to see who you play, on what and for what, then the first question. It picks up the seats from matchmaking
// (the sky ring) and counts down in the duel badge's place. A ghost shows when it was recorded.
function Lobby({ snapshot, me, opponent, opponentName }: LobbyProps) {
  const { t, locale } = useLocale()
  const { match } = snapshot
  const now = useTicker(true)
  const left = Math.max(0, msToDeadline(snapshot, now))
  const seconds = Math.ceil(left / 1000)
  // Reduced motion: the ring steps once a second instead of sweeping.
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches
  const ring = Math.min(1, (reduced ? seconds * 1000 : left) / (LOBBY_SECONDS * 1000))
  const rated = match.kind === 'ranked' || match.kind === 'ghost' // the only kinds that move the rating (docs §4)

  const side = (player: PlayerDto | undefined, name: string) => (
    <div className="grid min-w-0 justify-items-center gap-1">
      {player?.kind === 'bot' ? (
        <PlayerAvatar player={player} className={seat} />
      ) : (
        <SeatFrame>
          <PlayerAvatar player={player} className="absolute inset-1.5" />
          {player?.kind === 'ghost' && (
            <Ghost aria-hidden="true" className="absolute -right-1 -bottom-1 size-7 rounded-full bg-card p-1 text-primary" />
          )}
        </SeatFrame>
      )}
      <p className={cn('mt-2 max-w-full font-heading text-lg leading-tight font-semibold text-balance', nameClass(player?.user ?? {}))}>
        {name}
      </p>
      {player?.user?.title && <p className="max-w-full truncate text-xs font-medium text-primary">{player.user.title}</p>}
      {player?.user ? (
        <p className="flex flex-wrap items-center justify-center gap-x-1.5 font-mono text-xs text-muted-foreground tabular-nums">
          <TierIcon tier={player.user.tier} className="size-4" />
          {t.dashboard.tiers[player.user.tier]}
          <span aria-hidden="true">·</span>
          {ratingText(player.displayRating, player.user.isProvisional, t)}
        </p>
      ) : (
        player && <p className="font-mono text-xs text-muted-foreground tabular-nums">{player.displayRating}</p>
      )}
      {player?.persona && <p className="text-xs text-pretty text-muted-foreground">{t.play.traits[player.persona]}</p>}
      {player?.kind === 'ghost' && player.ghostRecordedAt && (
        <p className="text-xs text-pretty text-muted-foreground">{t.match.ghostRecorded(relativeTime(player.ghostRecordedAt, locale))}</p>
      )}
    </div>
  )

  // -mt-2 centers the disc on the portraits next to it (both grow by the same step at sm).
  const countdown = Number.isFinite(left) && (
    <div role="timer" className="-mt-2 grid justify-items-center gap-2">
      <span aria-hidden="true" className="relative grid size-20 place-items-center rounded-full bg-secondary sm:size-24">
        <svg viewBox="0 0 100 100" className="absolute inset-0 -rotate-90">
          <circle
            cx="50"
            cy="50"
            r="47"
            fill="none"
            strokeWidth="4"
            strokeLinecap="round"
            pathLength={100}
            strokeDasharray="100"
            strokeDashoffset={100 - ring * 100}
            className="stroke-primary transition-[stroke-dashoffset] duration-100 ease-linear motion-reduce:transition-none"
          />
        </svg>
        <span
          key={seconds}
          className="font-heading text-4xl font-semibold tabular-nums animate-in fade-in zoom-in-90 duration-200 ease-out motion-reduce:animate-none sm:text-5xl"
        >
          {seconds}
        </span>
      </span>
      <span aria-hidden="true" className="text-xs text-muted-foreground">
        {t.match.startsIn}
      </span>
      <span className="sr-only">{`${t.match.startsIn} ${seconds}`}</span>
    </div>
  )

  return (
    <section className="grid min-h-[70svh] content-center justify-items-center gap-10 text-center">
      <header className="grid justify-items-center gap-3">
        <p className="font-mono text-xs tracking-wide text-muted-foreground uppercase">{t.match.kinds[match.kind]}</p>
        <h2 className="flex items-center gap-2 font-heading text-2xl font-semibold tracking-tight text-balance sm:text-3xl">
          {createElement(topicIcon(match.topic?.slug ?? ''), { 'aria-hidden': true, className: 'size-6 shrink-0 text-primary sm:size-7' })}
          {match.topic?.name}
        </h2>
        {opponent && (
          <p className="flex flex-wrap justify-center gap-2">
            <Badge variant="secondary" className="h-7 px-3 font-mono">
              {t.match.questionCount(QUESTIONS_PER_MATCH)}
            </Badge>
            <Badge variant={rated ? 'default' : 'secondary'} className="h-7 px-3">
              {rated ? (me?.user?.isProvisional ? t.match.placementDuel : t.dashboard.play.rankedHint) : t.match.unrated}
            </Badge>
          </p>
        )}
      </header>
      {opponent ? (
        <div className="grid w-full max-w-lg grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-start gap-3 sm:gap-6">
          {side(me, t.match.you)}
          {countdown}
          {side(opponent, opponentName)}
        </div>
      ) : (
        <div className="grid justify-items-center gap-8">
          {side(me, t.match.you)}
          {countdown}
        </div>
      )}
    </section>
  )
}

interface ScoreboardProps {
  me?: PlayerDto
  opponent?: PlayerDto
  opponentName: string
  meAnswered: boolean
  opponentAnswered: boolean
}

// A quiet surface (the question holds the only sky ring): who plays, who has answered, and the score, the trailing side
// muted. Under each name the most urgent status: offline, answered, then the tier (a bot's rating).
function Scoreboard({ me, opponent, opponentName, meAnswered, opponentAnswered }: ScoreboardProps) {
  const { t } = useLocale()
  const side = (player: PlayerDto | undefined, name: string, answered: boolean, trailing: boolean, end?: boolean) => {
    const offline = player?.kind === 'human' && !player.connected
    return (
      <div className={cn('flex min-w-0 items-center gap-2 sm:gap-3', end && 'flex-row-reverse text-right')}>
        <PlayerAvatar player={player} className={cn('size-8 sm:size-10', offline && 'opacity-50')} />
        <div className={cn('grid min-w-0 gap-0.5', end && 'justify-items-end')}>
          <p className="max-w-full truncate text-sm font-medium">{name}</p>
          {offline ? (
            <p className="flex items-center gap-1 text-xs text-warning">
              <WifiOff aria-hidden="true" className="size-3" />
              {t.match.disconnected}
            </p>
          ) : answered ? (
            <p className="flex items-center gap-1 font-mono text-xs text-success">
              <Check aria-hidden="true" className="size-3" />
              {t.match.answered}
            </p>
          ) : player?.user ? (
            <p className="flex max-w-full items-center gap-1.5 text-xs text-muted-foreground">
              <TierIcon tier={player.user.tier} className="size-4" />
              <span className="truncate">{t.dashboard.tiers[player.user.tier]}</span>
            </p>
          ) : (
            player && <p className="font-mono text-xs text-muted-foreground tabular-nums">{player.displayRating}</p>
          )}
        </div>
        <p
          className={cn(
            'font-heading text-2xl font-semibold tabular-nums sm:text-3xl',
            end ? 'mr-auto' : 'ml-auto',
            trailing && 'text-muted-foreground',
          )}
        >
          {player?.score ?? 0}
        </p>
      </div>
    )
  }
  const surface = 'rounded-xl bg-card p-3 ring-1 ring-foreground/10 sm:p-4'

  if (!opponent) return <div className={cn('grid', surface)}>{side(me, t.match.you, meAnswered, false)}</div>

  const mine = me?.score ?? 0
  const theirs = opponent.score
  return (
    <div className={cn('grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-3', surface)}>
      {side(me, t.match.you, meAnswered, mine < theirs)}
      <span className="font-mono text-xs text-muted-foreground">vs</span>
      {side(opponent, opponentName, opponentAnswered, theirs < mine, true)}
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
      className={cn('ml-auto h-10 shrink-0 sm:h-8', !confirming && 'text-muted-foreground hover:text-destructive')}
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
  me?: PlayerDto
  opponent?: PlayerDto
  opponentName: string
}

// One question, keyed by its index so local picks and the timer start fresh for every question.
function QuestionStage({ snapshot, command, me, opponent, opponentName }: QuestionStageProps) {
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
  // Untimed practice waits for the player; a bot duel takes the button too, but moves on by itself at the deadline.
  const canNext = !!interstitial && (!match.phaseDeadline || match.kind === 'bot')
  const now = useTicker(match.timed && (!interstitial || canNext))
  const limit = turn.question.timeLimitMs
  const left = interstitial
    ? 0
    : match.timed
      ? Math.max(0, Math.min(limit - (now - startedAt), msToDeadline(snapshot, now) - 500))
      : Infinity
  // Untimed practice: no deadline in the interstitial, so the explanation has all the time it needs.
  const waitsForNext = canNext && !match.phaseDeadline
  const nextIn = canNext && match.phaseDeadline ? Math.ceil(Math.max(0, msToDeadline(snapshot, now)) / 1000) : undefined

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

  // The sky ring says "your move": it fades once the answer is in, the time is up or the key shows.
  const open = !answered && left > 0 && !interstitial

  return (
    <section className="grid gap-3">
      {match.timed && <TimerBar left={left} total={limit} hidden={!!interstitial} done={answered} />}
      <Card className={cn('transition-[box-shadow] duration-200 ease-out motion-reduce:transition-none', open && skyOutline)}>
        <CardContent>
          <QuestionBody
            content={turn.question.content}
            language={turn.question.language}
            mine={mine}
            correct={interstitial?.key.answer}
            locked={answered || left <= 0 || !!interstitial}
            me={me}
            rival={
              interstitial?.opponent ? { player: opponent, name: opponentName, payload: interstitial.opponent.payload } : undefined
            }
            onAnswer={answer}
          />
        </CardContent>
        {/* The reveal belongs to the question: the round's result, the line to remember, in practice the explanation. */}
        {interstitial && (
          <CardFooter className="grid gap-4 bg-transparent">
            <RoundResult me={me} opponent={opponent} opponentName={opponentName} mine={interstitial.mine} theirs={interstitial.opponent} />
            <Remember text={interstitial.key.remember} />
            {waitsForNext && <Explanation questionId={turn.question.id} />}
            {canNext && (
              <div className="grid gap-2 sm:flex sm:items-center sm:justify-end sm:gap-4">
                {nextIn !== undefined && (
                  <p role="timer" className="text-center font-mono text-xs text-muted-foreground tabular-nums">
                    {t.match.nextIn(nextIn)}
                  </p>
                )}
                <Button size="lg" className="w-full sm:w-auto" aria-disabled={moving} onClick={() => !moving && next()}>
                  {t.match.next}
                </Button>
              </div>
            )}
          </CardFooter>
        )}
      </Card>
      {!interstitial && (
        <p aria-live="polite" className="min-h-5 text-center text-sm text-muted-foreground">
          {answered && opponentName ? t.match.waiting(opponentName) : left <= 0 ? t.match.timeUp : ''}
        </p>
      )}
    </section>
  )
}

// Quiet until it matters: grey seconds, then red and bold under 5 s with one announcement. After my answer the time left
// is the opponent's, so the bar greys out and never turns urgent.
function TimerBar({ left, total, hidden, done }: { left: number; total: number; hidden: boolean; done: boolean }) {
  const { t } = useLocale()
  const seconds = Math.ceil(left / 1000)
  const urgent = !done && left <= 5000

  return (
    <div className={cn('flex items-center gap-3', hidden && 'invisible')}>
      <div aria-hidden="true" className="h-1.5 flex-1 overflow-hidden rounded-full bg-secondary">
        <div
          className={cn(
            'h-full rounded-full transition-[width,background-color] duration-100 ease-linear motion-reduce:transition-none',
            done ? 'bg-muted-foreground/40' : urgent ? 'bg-destructive' : 'bg-primary',
          )}
          style={{ width: `${(left / total) * 100}%` }}
        />
      </div>
      <span
        className={cn(
          'flex w-14 items-center justify-end gap-1 font-mono text-sm text-muted-foreground tabular-nums',
          urgent && 'font-semibold text-destructive',
        )}
      >
        <Timer aria-hidden="true" className="size-3.5" />
        <span aria-hidden="true">{t.match.veto.seconds(seconds)}</span>
        <span className="sr-only">{t.match.secondsLeft(seconds)}</span>
      </span>
      <p aria-live="polite" className="sr-only">
        {urgent && !hidden ? t.match.hurry : ''}
      </p>
    </div>
  )
}
