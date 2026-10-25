import { Check, Minus, Sparkles, X } from 'lucide-react'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { AnswerLabel, AnswerResultDto, PlayerDto } from '@/types/api'
import { PlayerAvatar } from './PlayerAvatar'

const LABEL_STYLES: Record<AnswerLabel, { icon: typeof Check; tone: string; fill: string }> = {
  brilliant: { icon: Sparkles, tone: 'text-primary', fill: 'bg-primary/15' },
  good: { icon: Check, tone: 'text-success', fill: 'bg-success/15' },
  miss: { icon: Minus, tone: 'text-muted-foreground', fill: 'bg-secondary' },
  blunder: { icon: X, tone: 'text-destructive', fill: 'bg-destructive/15' },
}

export function AnswerChip({ who, result }: { who?: string; result: AnswerResultDto }) {
  const { t } = useLocale()
  const { icon: Icon, tone, fill } = LABEL_STYLES[result.label]

  return (
    <span className={cn('inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-sm font-medium', fill, tone)}>
      <Icon aria-hidden="true" className="size-4" />
      {who && `${who}: `}
      {result.timedOut ? t.match.timedOut : t.match.labels[result.label]}
      <span className="font-mono text-xs">+{result.points}</span>
    </span>
  )
}

interface RoundResultProps {
  me?: PlayerDto
  opponent?: PlayerDto
  opponentName: string
  mine: AnswerResultDto
  theirs?: AnswerResultDto | null
}

// A round in a duel's reveal: me on the left and the opponent on the right, like the scoreboard above.
export function RoundResult({ me, opponent, opponentName, mine, theirs }: RoundResultProps) {
  const { t } = useLocale()
  return (
    <div className={cn('grid gap-3', theirs && 'grid-cols-2')}>
      <RoundSide player={me} name={t.match.you} result={mine} />
      {theirs && <RoundSide player={opponent} name={opponentName} result={theirs} end />}
    </div>
  )
}

// The points large, then the label (icon, color and text) and how long the answer took.
function RoundSide({ player, name, result, end }: { player?: PlayerDto; name: string; result: AnswerResultDto; end?: boolean }) {
  const { t } = useLocale()
  const { icon: Icon, tone } = LABEL_STYLES[result.label]

  return (
    <div className={cn('flex min-w-0 items-center gap-2.5', end && 'flex-row-reverse text-right')}>
      <PlayerAvatar player={player} className="size-8" />
      <div className={cn('grid min-w-0 gap-1', end && 'justify-items-end')}>
        <p className={cn('font-heading text-2xl leading-none font-semibold tabular-nums', result.points ? tone : 'text-muted-foreground')}>
          <span className="sr-only">{name}: </span>+{result.points}
        </p>
        <p className={cn('flex flex-wrap items-center gap-x-1 text-xs', end && 'justify-end')}>
          <span className={cn('flex items-center gap-1 font-medium', tone)}>
            <Icon aria-hidden="true" className="size-3.5 shrink-0" />
            {result.timedOut ? t.match.timedOut : t.match.labels[result.label]}
          </span>
          {!result.timedOut && <span className="font-mono text-muted-foreground tabular-nums">· {t.match.answerTime(result.elapsedMs)}</span>}
        </p>
      </div>
    </div>
  )
}
