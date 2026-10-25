import { ArrowRight } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { Option } from '@/types/api'
import { MarkIcon } from './OptionList'
import { badge, badgeStyles, idleHover, markStyles, type Mark } from './question-utils'

type Pairs = { leftId: string; rightId: string }[]

interface MatchPairsProps {
  left: Option[]
  right: Option[]
  mine?: Pairs
  correct?: Pairs
  locked: boolean
  onAnswer: (pairs: Pairs) => void
}

const tile =
  'flex min-h-12 w-full items-center gap-2.5 rounded-lg px-3 py-2.5 text-left text-sm font-medium ring-inset transition-[color,background-color,box-shadow] duration-150 ease-out motion-reduce:transition-none'

// Tap a left item, then its match on the right; a number ties the two together. Locked, it lists the pairs.
export function MatchPairs({ left, right, mine, correct, locked, onAnswer }: MatchPairsProps) {
  const { t } = useLocale()
  // One state object updated functionally, so two quick taps never work from a stale pick.
  const [{ pairs, active }, setPicks] = useState<{ pairs: Record<string, string>; active?: string }>({
    pairs: {},
    active: left[0]?.id,
  })
  const number = (leftId: string) => left.findIndex((item) => item.id === leftId) + 1
  const text = (options: Option[], id?: string) => options.find((option) => option.id === id)?.text ?? ''

  if (locked) {
    const shown = mine ?? correct ?? []
    return (
      <ul className="grid gap-2">
        {left.map((item) => {
          const rightId = shown.find((pair) => pair.leftId === item.id)?.rightId
          const key = correct?.find((pair) => pair.leftId === item.id)?.rightId
          const mark: Mark = !correct ? 'idle' : !mine ? 'correct' : rightId === key ? 'correct' : 'wrong'
          return (
            <li key={item.id} className={cn(tile, 'flex-wrap', markStyles[mark])}>
              <span className="font-mono text-[0.8125rem]">{item.text}</span>
              <ArrowRight aria-hidden="true" className="size-4 shrink-0 opacity-70" />
              <span className="min-w-0 flex-1">
                {text(right, rightId)}
                {mark === 'wrong' && (
                  <span className="block text-xs text-foreground/80">
                    {t.match.correctAnswer}: {text(right, key)}
                  </span>
                )}
              </span>
              <MarkIcon mark={mark} />
            </li>
          )
        })}
      </ul>
    )
  }

  const pick = (rightId: string) =>
    setPicks((previous) => {
      if (!previous.active) return previous
      const next = Object.fromEntries(Object.entries(previous.pairs).filter(([, id]) => id !== rightId))
      next[previous.active] = rightId
      return { pairs: next, active: left.find((item) => !next[item.id])?.id }
    })

  const pairBadge = (n: number) => (
    <span aria-hidden="true" className={cn(badge, badgeStyles.picked)}>
      {n}
    </span>
  )

  return (
    <div className="grid gap-4">
      <p className="text-sm text-muted-foreground">{t.match.hints.pairs}</p>
      <div className="grid grid-cols-[minmax(0,2fr)_minmax(0,3fr)] gap-3">
        <ul className="grid content-start gap-2">
          {left.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                aria-pressed={active === item.id}
                onClick={() => setPicks((previous) => ({ ...previous, active: item.id }))}
                className={cn(
                  tile,
                  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
                  active === item.id ? markStyles.picked : cn(markStyles.idle, idleHover),
                )}
              >
                {pairs[item.id] ? pairBadge(number(item.id)) : <span aria-hidden="true" className={cn(badge, badgeStyles.idle)} />}
                <span className="min-w-0 font-mono text-[0.8125rem] break-words">{item.text}</span>
              </button>
            </li>
          ))}
        </ul>
        <ul className="grid content-start gap-2">
          {right.map((item) => {
            const owner = Object.keys(pairs).find((leftId) => pairs[leftId] === item.id)
            return (
              <li key={item.id}>
                <button
                  type="button"
                  disabled={!active}
                  onClick={() => pick(item.id)}
                  className={cn(
                    tile,
                    'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:cursor-default',
                    owner ? markStyles.picked : cn(markStyles.idle, idleHover),
                  )}
                >
                  {owner && pairBadge(number(owner))}
                  <span className="min-w-0 break-words">{item.text}</span>
                </button>
              </li>
            )
          })}
        </ul>
      </div>
      <Button
        size="lg"
        className="justify-self-end"
        disabled={Object.keys(pairs).length < left.length}
        onClick={() => onAnswer(left.map((item) => ({ leftId: item.id, rightId: pairs[item.id] })))}
      >
        {t.match.confirm}
      </Button>
    </div>
  )
}
