import { Check } from 'lucide-react'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'

// "What does it print" example from the game idea; [1, 10, 2] is right because sort() compares strings.
const OPTIONS = [
  { text: '[1, 2, 10]', correct: false },
  { text: '[1, 10, 2]', correct: true },
  { text: '[10, 2, 1]', correct: false },
  { text: 'TypeError', correct: false },
]

// A still frame of a duel for the auth screens. Decorative: the headline next to it carries the message.
export function DuelPreview() {
  const { t } = useLocale()
  const copy = t.auth.preview

  return (
    <div aria-hidden="true" className="grid max-w-xl gap-3 select-none">
      <div className="grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-4 rounded-xl bg-background px-5 py-4">
        <Player name={copy.you} rating={1012} />
        <div className="grid justify-items-center gap-0.5">
          <span className="font-heading text-2xl font-semibold tabular-nums">
            640 <span className="text-muted-foreground">: 350</span>
          </span>
          <span className="font-mono text-xs font-medium text-muted-foreground">{copy.round}</span>
        </div>
        <Player name={copy.opponent} rating={1000} alignEnd />
      </div>

      <div className="grid gap-4 rounded-xl bg-background p-5">
        <div className="flex items-center justify-between font-mono text-xs font-medium tracking-wide uppercase text-muted-foreground">
          <span>{copy.questionType}</span>
          <span className="text-foreground tabular-nums">0:14</span>
        </div>
        <div className="h-1 rounded-full bg-secondary">
          <div className="h-full w-3/5 rounded-full bg-chart-1" />
        </div>
        <pre className="overflow-x-auto rounded-lg bg-secondary px-4 py-3 font-mono text-sm">
          <code>console.log([1, 2, 10].sort())</code>
        </pre>
        <ul className="grid grid-cols-2 gap-2 font-mono text-sm">
          {OPTIONS.map((option) => (
            <li
              key={option.text}
              className={cn(
                'flex items-center justify-between rounded-lg px-3 py-2.5 ring-1 ring-input/60 ring-inset',
                option.correct && 'bg-primary text-primary-foreground ring-primary',
              )}
            >
              {option.text}
              {option.correct && <Check className="size-4" />}
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

function Player({ name, rating, alignEnd }: { name: string; rating: number; alignEnd?: boolean }) {
  return (
    <div className={cn('flex min-w-0 items-center gap-3', alignEnd && 'flex-row-reverse text-right')}>
      <span className="grid size-10 shrink-0 place-items-center rounded-full bg-secondary font-heading font-semibold">
        {name.charAt(0)}
      </span>
      <div className="grid min-w-0">
        <span className="truncate text-sm font-medium">{name}</span>
        <span className="font-mono text-xs text-muted-foreground tabular-nums">{rating}</span>
      </div>
    </div>
  )
}
