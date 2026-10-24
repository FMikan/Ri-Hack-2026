import { ArrowDown, ArrowUp } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { Option } from '@/types/api'
import { MarkIcon } from './OptionList'
import { markStyles, type Mark } from './question-utils'

interface OrderStepsProps {
  steps: Option[]
  mine?: string[]
  correct?: string[]
  locked: boolean
  onAnswer: (orderedIds: string[]) => void
}

const row = 'flex min-h-12 items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium ring-1 ring-inset'

// Up and down buttons instead of drag and drop: they work the same with a thumb, a mouse and a keyboard.
export function OrderSteps({ steps, mine, correct, locked, onAnswer }: OrderStepsProps) {
  const { t } = useLocale()
  const [order, setOrder] = useState(() => steps.map((step) => step.id))
  const text = (id: string) => steps.find((step) => step.id === id)?.text ?? id

  if (locked) {
    const shown = mine ?? correct ?? order
    return (
      <ol className="grid gap-2">
        {shown.map((id, index) => {
          const mark: Mark = !correct ? 'idle' : correct[index] === id ? 'correct' : 'wrong'
          return (
            <li key={id} className={cn(row, markStyles[mark])}>
              <span className="w-5 shrink-0 font-mono text-xs opacity-70">{index + 1}</span>
              <span className="min-w-0 flex-1 break-words">{text(id)}</span>
              {mark === 'wrong' && <span className="shrink-0 font-mono text-xs">→ {correct!.indexOf(id) + 1}</span>}
              <MarkIcon mark={mark} />
            </li>
          )
        })}
      </ol>
    )
  }

  const move = (from: number, to: number) =>
    setOrder((previous) => {
      const next = [...previous]
      ;[next[from], next[to]] = [next[to], next[from]]
      return next
    })

  const arrow = 'grid size-10 shrink-0 place-items-center rounded-md text-muted-foreground transition-colors hover:bg-background/60 hover:text-foreground focus-visible:outline-2 focus-visible:outline-ring disabled:opacity-30'

  return (
    <div className="grid gap-4">
      <p className="text-sm text-muted-foreground">{t.match.hints.order}</p>
      <ol className="grid gap-2">
        {order.map((id, index) => (
          <li key={id} className={cn(row, 'bg-secondary py-1 pr-1 ring-border')}>
            <span className="w-5 shrink-0 font-mono text-xs text-muted-foreground">{index + 1}</span>
            <span className="min-w-0 flex-1 break-words">{text(id)}</span>
            <button type="button" aria-label={t.match.moveUp(text(id))} disabled={index === 0} onClick={() => move(index, index - 1)} className={arrow}>
              <ArrowUp aria-hidden="true" className="size-4" />
            </button>
            <button
              type="button"
              aria-label={t.match.moveDown(text(id))}
              disabled={index === order.length - 1}
              onClick={() => move(index, index + 1)}
              className={arrow}
            >
              <ArrowDown aria-hidden="true" className="size-4" />
            </button>
          </li>
        ))}
      </ol>
      <Button size="lg" className="justify-self-end" onClick={() => onAnswer(order)}>
        {t.match.confirm}
      </Button>
    </div>
  )
}
