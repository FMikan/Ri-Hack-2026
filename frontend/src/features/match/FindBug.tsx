import { useState } from 'react'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { Option, PlayerDto } from '@/types/api'
import { CodeBlock } from './CodeBlock'
import { OptionList, type Rival } from './OptionList'

type Answer = { line: number; fixId: string }

interface FindBugProps {
  code: string
  language?: string | null
  fixes: Option[]
  mine?: Answer
  correct?: Answer & { acceptLines?: number[] }
  locked: boolean
  me?: PlayerDto
  rival?: Rival
  onAnswer: (answer: Answer) => void
}

// Two steps: tap the line (1-based), then pick the fix, which sends the answer.
export function FindBug({ code, language, fixes, mine, correct, locked, me, rival, onAnswer }: FindBugProps) {
  const { t } = useLocale()
  const [line, setLine] = useState<number>()
  const chosen = mine?.line ?? line
  const bugLines = correct ? [correct.line, ...(correct.acceptLines ?? [])] : []

  return (
    <div className="grid gap-4">
      {!locked && <p className="text-sm text-muted-foreground">{t.match.hints.bug}</p>}
      <CodeBlock
        code={code}
        language={language}
        line={(content, number) => {
          const tone = bugLines.includes(number)
            ? 'bg-success/15 text-success'
            : correct && number === chosen
              ? 'bg-destructive/15 text-destructive'
              : number === chosen
                ? 'bg-primary/15 text-primary'
                : ''
          return (
            <button
              type="button"
              aria-label={t.match.line(number)}
              aria-pressed={number === chosen}
              disabled={locked}
              onClick={() => setLine(number)}
              className={cn(
                'col-span-2 grid grid-cols-subgrid text-left focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring enabled:hover:bg-secondary disabled:cursor-default',
                tone,
              )}
            >
              <span aria-hidden="true" className="pr-3 text-right opacity-60 select-none">
                {number}
              </span>
              <code className="pr-4 whitespace-pre">{content}</code>
            </button>
          )
        }}
      />
      {(chosen !== undefined || correct) && (
        <OptionList
          options={fixes}
          picked={mine?.fixId}
          correct={correct?.fixId}
          locked={locked || chosen === undefined}
          me={me}
          rival={rival}
          onPick={(fixId) => chosen !== undefined && onAnswer({ line: chosen, fixId })}
        />
      )}
    </div>
  )
}
