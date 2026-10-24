import { Ban } from 'lucide-react'
import { createElement, useState } from 'react'
import { DuelBadge } from '@/components/shared/RankedDuelIcon'
import { Badge } from '@/components/ui/badge'
import { useTicker } from '@/hooks/use-ticker'
import { api } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import { cn } from '@/lib/utils'
import type { PlayerDto } from '@/types/api'
import { PlayerAvatar } from './PlayerAvatar'
import { msToDeadline, type Command, type Snapshot } from './snapshot'

interface VetoViewProps {
  snapshot: Snapshot
  command: Command
  me?: PlayerDto
  opponent?: PlayerDto
  opponentName: string
}

const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

// Ranked and friend duels pick their topic first (game idea §4): a coin toss decides who bans first, the players take
// turns banning from the pool (8 s each, a timeout bans at random), and one of the last two is drawn. The server knows
// every result in advance; the coin and the draw only animate towards it.
export function VetoView({ snapshot, command, me, opponent, opponentName }: VetoViewProps) {
  const { t } = useLocale()
  const copy = t.match.veto
  const { match } = snapshot
  const veto = match.veto!
  const now = useTicker(true)
  const left = Math.max(0, msToDeadline(snapshot, now))
  const [banning, setBanning] = useState<string>()
  const myTurn = match.phase === 'veto_ban' && veto.turnSeat === match.mySeat
  // Both animations take the first part of their 3 s phase, then hold the result.
  const settled = reducedMotion() || left < 1500

  async function ban(topicId: string) {
    if (!myTurn || banning) return
    setBanning(topicId)
    await command(() => api.ban(match.id, { topicId }))
    setBanning(undefined)
  }

  const header = (
    <div className="flex items-center justify-between gap-3 rounded-xl bg-card p-3 ring-1 ring-foreground/10 sm:p-4">
      <span className="flex min-w-0 items-center gap-3">
        <PlayerAvatar player={me} className="size-10" />
        <span className="truncate text-sm font-medium">{t.match.you}</span>
      </span>
      <span className="font-mono text-xs text-muted-foreground">vs</span>
      <span className="flex min-w-0 flex-row-reverse items-center gap-3">
        <PlayerAvatar player={opponent} className="size-10" />
        <span className="truncate text-sm font-medium">{opponentName}</span>
      </span>
    </div>
  )

  if (match.phase === 'veto_coin') {
    const winner = veto.coinWinnerSeat === match.mySeat ? me : opponent
    return (
      <section className="grid gap-5">
        {header}
        <div className="grid min-h-[50svh] place-content-center justify-items-center gap-5 text-center">
          <p className="text-sm text-muted-foreground">{copy.coin}</p>
          {settled ? (
            <PlayerAvatar player={winner} className="size-24 ring-4 ring-primary" />
          ) : (
            <DuelBadge className="size-24 animate-coin motion-reduce:animate-none" />
          )}
          <p aria-live="polite" className="font-heading text-2xl font-semibold tracking-tight">
            {settled ? (veto.coinWinnerSeat === match.mySeat ? copy.youFirst : copy.theyFirst(opponentName)) : copy.tossing}
          </p>
        </div>
      </section>
    )
  }

  const picking = match.phase === 'veto_pick'
  const finalTwo = veto.finalTwo ?? []
  // The draw flicks between the last two topics, then stops on the one the server chose.
  const highlighted = picking ? (settled || finalTwo.length < 2 ? veto.chosenTopicId : finalTwo[Math.floor(now / 250) % 2]) : undefined
  const chosen = veto.pool.find((entry) => entry.topic.id === veto.chosenTopicId)?.topic
  const seconds = Math.ceil(left / 1000)

  return (
    <section className="grid gap-5">
      {header}
      <div className="grid gap-1">
        <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
          <h2 aria-live="polite" className="font-heading text-xl font-semibold tracking-tight">
            {picking
              ? settled && chosen
                ? copy.topicIs(chosen.name)
                : copy.picking
              : myTurn
                ? copy.yourTurn
                : copy.theirTurn(opponentName)}
          </h2>
          {!picking && Number.isFinite(seconds) && (
            <span role="timer" className={cn('ml-auto font-mono text-sm tabular-nums', seconds <= 3 && 'text-destructive')}>
              {copy.seconds(seconds)}
            </span>
          )}
        </div>
        <p className="text-sm text-pretty text-muted-foreground">{picking ? copy.pickHint : copy.banHint(veto.bansRemaining)}</p>
      </div>
      <ul className="grid gap-2 sm:grid-cols-2">
        {veto.pool.map((entry) => {
          const banned = entry.bannedBySeat != null
          const canBan = myTurn && !banned && !banning
          const lit = highlighted === entry.topic.id
          return (
            <li key={entry.topic.id}>
              <button
                type="button"
                disabled={!canBan}
                onClick={() => ban(entry.topic.id)}
                aria-label={canBan ? copy.ban(entry.topic.name) : undefined}
                className={cn(
                  'group/tile flex w-full items-center gap-3 rounded-lg p-3.5 text-left ring-1 ring-inset transition-colors',
                  banned
                    ? 'bg-secondary/40 text-muted-foreground ring-border'
                    : lit
                      ? 'bg-primary/10 ring-2 ring-primary'
                      : 'bg-secondary ring-border',
                  canBan &&
                    'cursor-pointer hover:bg-destructive/10 hover:ring-destructive focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
                  banning === entry.topic.id && 'bg-destructive/10 ring-destructive',
                )}
              >
                {createElement(topicIcon(entry.topic.slug), {
                  'aria-hidden': true,
                  className: cn('size-5 shrink-0', banned ? 'text-muted-foreground' : 'text-primary'),
                })}
                <span className="grid min-w-0 flex-1 gap-0.5">
                  <span className={cn('truncate font-medium', banned && 'line-through')}>{entry.topic.name}</span>
                  <span className="text-xs text-muted-foreground">
                    {banned
                      ? entry.auto
                        ? copy.bannedAuto
                        : entry.bannedBySeat === match.mySeat
                          ? copy.bannedByYou
                          : copy.bannedBy(opponentName)
                      : entry.myMastery == null
                        ? copy.notPlayed
                        : copy.mastery(Math.round(entry.myMastery))}
                  </span>
                </span>
                {entry.pickedByOpponent && !banned && <Badge variant="outline">{copy.theirs}</Badge>}
                {canBan && (
                  <Ban
                    aria-hidden="true"
                    className="size-4 shrink-0 text-destructive opacity-0 transition-opacity group-hover/tile:opacity-100 group-focus-visible/tile:opacity-100"
                  />
                )}
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
