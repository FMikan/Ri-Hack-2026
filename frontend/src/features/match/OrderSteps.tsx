import { ArrowDown, ArrowUp } from 'lucide-react'
import { useLayoutEffect, useRef, useState } from 'react'
import { Button } from '@/components/ui/button'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { Option } from '@/types/api'
import { MarkIcon } from './OptionList'
import { badge, badgeStyles, markStyles, type Mark } from './question-utils'

interface OrderStepsProps {
  steps: Option[]
  mine?: string[]
  correct?: string[]
  locked: boolean
  onAnswer: (orderedIds: string[]) => void
}

const row = 'flex min-h-12 items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium ring-inset'

// Up and down buttons instead of drag and drop: they work the same with a thumb, a mouse and a keyboard.
export function OrderSteps({ steps, mine, correct, locked, onAnswer }: OrderStepsProps) {
  const { t } = useLocale()
  const [order, setOrder] = useState(() => steps.map((step) => step.id))
  const [moved, setMoved] = useState<string>() // the step moved last keeps a sky outline, so it is easy to follow
  const rows = useRef(new Map<string, HTMLLIElement>())
  const before = useRef(new Map<string, number>()) // where the two swapped rows were before the swap
  const text = (id: string) => steps.find((step) => step.id === id)?.text ?? id

  // The two swapped rows slide from where they were to where they are (FLIP). A swap mid-slide starts from where the
  // row is drawn, so quick taps never jump. With reduced motion the outline alone shows the move.
  useLayoutEffect(() => {
    if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches)
      for (const [id, top] of before.current) {
        const row = rows.current.get(id)
        const dy = row ? top - row.getBoundingClientRect().top : 0
        if (dy) row!.animate([{ transform: `translateY(${dy}px)` }, { transform: 'none' }], { duration: 180, easing: 'cubic-bezier(0.2, 0, 0, 1)' })
      }
    before.current.clear()
  }, [order])

  if (locked) {
    const shown = mine ?? correct ?? order
    return (
      <ol className="grid gap-2">
        {shown.map((id, index) => {
          const mark: Mark = !correct ? 'idle' : correct[index] === id ? 'correct' : 'wrong'
          return (
            <li key={id} className={cn(row, markStyles[mark])}>
              <span aria-hidden="true" className={cn(badge, badgeStyles[mark])}>
                {index + 1}
              </span>
              <span className="min-w-0 flex-1 break-words">{text(id)}</span>
              {mark === 'wrong' && <span className="shrink-0 font-mono text-xs">→ {correct!.indexOf(id) + 1}</span>}
              <MarkIcon mark={mark} />
            </li>
          )
        })}
      </ol>
    )
  }

  const move = (from: number, to: number) => {
    if (to < 0 || to >= order.length) return
    before.current = new Map([order[from], order[to]].map((id) => [id, rows.current.get(id)?.getBoundingClientRect().top ?? 0]))
    const next = [...order]
    ;[next[from], next[to]] = [next[to], next[from]]
    setOrder(next)
    setMoved(order[from])
  }

  // aria-disabled instead of disabled at the ends: a disabled button drops keyboard focus as the step reaches the edge.
  const arrow =
    'grid size-11 place-items-center text-foreground transition-[color,background-color,scale] duration-150 ease-out focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring not-aria-disabled:hover:bg-primary/10 not-aria-disabled:hover:text-primary not-aria-disabled:active:scale-[0.96] aria-disabled:cursor-default aria-disabled:text-muted-foreground/40 motion-reduce:transition-none sm:size-10'

  return (
    <div className="grid gap-4">
      <p className="text-sm text-muted-foreground">{t.match.hints.order}</p>
      <ol className="grid gap-2">
        {order.map((id, index) => {
          const first = index === 0
          const last = index === order.length - 1
          return (
            <li
              key={id}
              ref={(element) => {
                if (element) rows.current.set(id, element)
              }}
              className={cn(
                row,
                'py-1 pr-1 transition-[background-color,box-shadow] duration-150 ease-out motion-reduce:transition-none',
                id === moved ? markStyles.picked : markStyles.idle,
              )}
            >
              <span aria-hidden="true" className={cn(badge, badgeStyles.idle)}>
                {index + 1}
              </span>
              <span className="min-w-0 flex-1 break-words">{text(id)}</span>
              {/* One recessed control with two halves, so it reads as "move this step" rather than two loose icons. */}
              <span className="flex shrink-0 divide-x divide-input/50 overflow-hidden rounded-sm border border-input/50 bg-background/60">
                <button type="button" aria-label={t.match.moveUp(text(id))} aria-disabled={first} onClick={() => move(index, index - 1)} className={arrow}>
                  <ArrowUp aria-hidden="true" className="size-4" />
                </button>
                <button type="button" aria-label={t.match.moveDown(text(id))} aria-disabled={last} onClick={() => move(index, index + 1)} className={arrow}>
                  <ArrowDown aria-hidden="true" className="size-4" />
                </button>
              </span>
            </li>
          )
        })}
      </ol>
      <Button size="lg" className="justify-self-end" onClick={() => onAnswer(order)}>
        {t.match.confirm}
      </Button>
    </div>
  )
}
