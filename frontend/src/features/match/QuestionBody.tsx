import { useLocale } from '@/lib/i18n/locale-context'
import type { AnswerKeyDto, AnswerPayload, QuestionContent } from '@/types/api'
import { CodeBlock } from './CodeBlock'
import { FindBug } from './FindBug'
import { MatchPairs } from './MatchPairs'
import { OptionList } from './OptionList'
import { OrderSteps } from './OrderSteps'
import { promptOf } from './question-utils'

interface QuestionBodyProps {
  content: QuestionContent
  mine?: AnswerPayload | null // what I sent; locks the question
  correct?: AnswerKeyDto['answer'] // the key, only in the interstitial
  locked: boolean
  onAnswer: (payload: AnswerPayload) => void
}

// The prompt and the answer controls for each of the 8 question types. The same view shows the key afterwards.
export function QuestionBody({ content, mine, correct, locked, onAnswer }: QuestionBodyProps) {
  const { t } = useLocale()
  const optionId = (payload?: AnswerPayload | null) => (payload && 'optionId' in payload ? payload.optionId : undefined)
  const choice = (options: { id: string; text: string }[], mono?: boolean) => (
    <OptionList
      options={options}
      picked={optionId(mine)}
      correct={optionId(correct)}
      locked={locked}
      mono={mono}
      onPick={(id) => onAnswer({ optionId: id })}
    />
  )

  return (
    <div className="grid gap-5">
      <h2 className="font-heading text-xl leading-snug font-semibold text-balance sm:text-2xl">{promptOf(content, t)}</h2>
      {content.type === 'true_false' ? (
        <OptionList
          options={[
            { id: 'true', text: t.match.yes },
            { id: 'false', text: t.match.no },
          ]}
          picked={mine && 'value' in mine ? String(mine.value) : undefined}
          correct={correct && 'value' in correct ? String(correct.value) : undefined}
          locked={locked}
          onPick={(id) => onAnswer({ value: id === 'true' })}
        />
      ) : content.type === 'multiple_choice' ? (
        <>
          {content.code && <CodeBlock code={content.code} />}
          {choice(content.options)}
        </>
      ) : content.type === 'cloze' || content.type === 'output' ? (
        <>
          <CodeBlock code={content.code} />
          {choice(content.options, true)}
        </>
      ) : content.type === 'odd_one_out' ? (
        <OptionList
          options={content.items}
          picked={mine && 'itemId' in mine ? mine.itemId : undefined}
          correct={correct && 'itemId' in correct ? correct.itemId : undefined}
          locked={locked}
          mono
          onPick={(id) => onAnswer({ itemId: id })}
        />
      ) : content.type === 'match_pairs' ? (
        <MatchPairs
          left={content.left}
          right={content.right}
          mine={mine && 'pairs' in mine ? mine.pairs : undefined}
          correct={correct && 'pairs' in correct ? correct.pairs : undefined}
          locked={locked}
          onAnswer={(pairs) => onAnswer({ pairs })}
        />
      ) : content.type === 'order' ? (
        <OrderSteps
          steps={content.steps}
          mine={mine && 'orderedIds' in mine ? mine.orderedIds : undefined}
          correct={correct && 'orderedIds' in correct ? correct.orderedIds : undefined}
          locked={locked}
          onAnswer={(orderedIds) => onAnswer({ orderedIds })}
        />
      ) : (
        <FindBug
          code={content.code}
          fixes={content.fixes}
          mine={mine && 'line' in mine ? mine : undefined}
          correct={correct && 'line' in correct ? correct : undefined}
          locked={locked}
          onAnswer={onAnswer}
        />
      )}
    </div>
  )
}
