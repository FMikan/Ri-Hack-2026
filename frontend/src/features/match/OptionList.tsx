import { Check, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Option } from '@/types/api'
import { markOf, markStyles, type Mark } from './question-utils'

// Text next to color: correct and wrong also get an icon.
export function MarkIcon({ mark }: { mark: Mark }) {
  if (mark === 'correct') return <Check aria-hidden="true" className="ml-auto size-4 shrink-0" />
  if (mark === 'wrong') return <X aria-hidden="true" className="ml-auto size-4 shrink-0" />
  return null
}

interface OptionListProps {
  options: Option[]
  picked?: string
  correct?: string
  locked: boolean
  mono?: boolean
  onPick: (id: string) => void
}

// One tap answers (multiple choice, cloze, output, odd one out, true/false).
export function OptionList({ options, picked, correct, locked, mono, onPick }: OptionListProps) {
  return (
    <ul className="grid gap-2.5 sm:grid-cols-2">
      {options.map((option, index) => {
        const mark = markOf(option.id, picked, correct)
        return (
          <li key={option.id}>
            <button
              type="button"
              aria-pressed={option.id === picked}
              disabled={locked}
              onClick={() => onPick(option.id)}
              className={cn(
                'flex min-h-14 w-full items-center gap-3 rounded-lg px-4 py-3 text-left text-sm font-medium ring-2 ring-inset transition-[color,background-color,box-shadow,scale] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring enabled:hover:ring-primary enabled:active:scale-[0.98] disabled:cursor-default',
                markStyles[mark],
              )}
            >
              <span
                aria-hidden="true"
                className="grid size-7 shrink-0 place-items-center rounded-md bg-background/60 font-mono text-xs text-muted-foreground"
              >
                {String.fromCharCode(65 + index)}
              </span>
              <span className={cn('min-w-0 break-words', mono && 'font-mono text-[0.8125rem]')}>{option.text}</span>
              <MarkIcon mark={mark} />
            </button>
          </li>
        )
      })}
    </ul>
  )
}
