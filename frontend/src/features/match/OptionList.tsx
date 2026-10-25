import { Check, X } from 'lucide-react'
import { createElement } from 'react'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { Option, PlayerDto } from '@/types/api'
import { PlayerAvatar } from './PlayerAvatar'
import { badge, badgeStyles, idleHover, markOf, markStyles, type Mark } from './question-utils'

// Text next to color: correct and wrong also get an icon.
export function MarkIcon({ mark }: { mark: Mark }) {
  if (mark === 'correct') return <Check aria-hidden="true" className="ml-auto size-4 shrink-0" />
  if (mark === 'wrong') return <X aria-hidden="true" className="ml-auto size-4 shrink-0" />
  return null
}

// The opponent in a duel's reveal and the option they picked.
export interface Rival {
  player?: PlayerDto
  name: string
  picked?: string
}

interface OptionListProps {
  options: Option[]
  picked?: string
  correct?: string
  locked: boolean
  mono?: boolean
  trueFalse?: boolean // check and cross badges instead of A and B
  me?: PlayerDto // in a duel's reveal, my portrait marks my pick and the rival's marks theirs
  rival?: Rival
  onPick: (id: string) => void
}

// The portraits pop in once when the key arrives; with reduced motion they just appear.
const portrait = 'size-7 shrink-0 animate-seat-reveal motion-reduce:animate-none'

// One tap answers (multiple choice, cloze, output, odd one out, true/false, the fix of find the bug).
export function OptionList({ options, picked, correct, locked, mono, trueFalse, me, rival, onPick }: OptionListProps) {
  const { t } = useLocale()
  const reveal = correct !== undefined

  return (
    // Every row as tall as the tallest and every tile filling its cell, so a short option never sits in a smaller box.
    <ul className="grid auto-rows-fr gap-2.5 sm:grid-cols-2">
      {options.map((option, index) => {
        const marked = markOf(option.id, picked, correct)
        // Once locked (answered or out of time), the options I did not pick recede.
        const mark = locked && marked === 'idle' ? 'dimmed' : marked
        const mine = option.id === picked
        return (
          <li key={option.id}>
            <button
              type="button"
              aria-pressed={mine}
              disabled={locked}
              onClick={() => onPick(option.id)}
              className={cn(
                'flex h-full min-h-14 w-full items-center gap-3 rounded-lg px-4 py-3 text-left text-sm font-medium ring-inset transition-[color,background-color,box-shadow,scale] duration-150 ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring enabled:active:scale-[0.98] disabled:cursor-default motion-reduce:transition-none',
                markStyles[mark],
                mark === 'idle' && idleHover,
              )}
            >
              {reveal && me && mine ? (
                <PlayerAvatar player={me} className={portrait} />
              ) : (
                <span aria-hidden="true" className={cn(badge, badgeStyles[mark])}>
                  {trueFalse ? createElement(index === 0 ? Check : X, { className: 'size-4' }) : String.fromCharCode(65 + index)}
                </span>
              )}
              <span className={cn('min-w-0 break-words', mono && 'font-mono text-[0.8125rem]')}>{option.text}</span>
              <span className="ml-auto flex shrink-0 items-center gap-2">
                {mine && !(reveal && me) && (
                  <span aria-hidden="true" className={cn('font-mono text-xs', mark === 'picked' && 'text-primary')}>
                    {t.match.you}
                  </span>
                )}
                <MarkIcon mark={mark} />
                {reveal && rival && option.id === rival.picked && (
                  <>
                    <PlayerAvatar player={rival.player} className={portrait} />
                    <span className="sr-only">{t.match.pickedBy(rival.name)}</span>
                  </>
                )}
              </span>
            </button>
          </li>
        )
      })}
    </ul>
  )
}
