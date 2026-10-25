import { useLocale } from '@/lib/i18n/locale-context'
import type { AnswerKeyDto, AnswerPayload, PlayerDto, QuestionContent } from '@/types/api'
import { CodeBlock } from './CodeBlock'
import { FindBug } from './FindBug'
import { MatchPairs } from './MatchPairs'
import { OptionList } from './OptionList'
import { OrderSteps } from './OrderSteps'
import { promptOf } from './question-utils'

interface QuestionBodyProps {
  content: QuestionContent
  language?: string | null // QuestionPublicDto.language, for syntax colors
  mine?: AnswerPayload | null // what I sent; locks the question
  correct?: AnswerKeyDto['answer'] // the key, only in the interstitial
  locked: boolean
  me?: PlayerDto // a duel's reveal marks both picks with the players' portraits
  rival?: { player?: PlayerDto; name: string; payload?: AnswerPayload | null }
  onAnswer: (payload: AnswerPayload) => void
}

// The tile a payload picked, for the types answered with one tap (find the bug: its fix).
const pickOf = (payload?: AnswerPayload | null) =>
  !payload
    ? undefined
    : 'optionId' in payload
      ? payload.optionId
      : 'itemId' in payload
        ? payload.itemId
        : 'value' in payload
          ? String(payload.value)
          : 'fixId' in payload
            ? payload.fixId
            : undefined

// The prompt and the answer controls for each of the 8 question types. The same view shows the key afterwards.
// Zoomed 5% so prompt, code and answers read larger than the surrounding UI; zoom (unlike scale) reflows, so phones
// don't scroll sideways.
export function QuestionBody({ content, language, mine, correct, locked, me, rival, onAnswer }: QuestionBodyProps) {
  const { t } = useLocale()
  // Every one-tap type shares the same picks, so all of them mark mine, the rival's and the key alike.
  const tiles = {
    picked: pickOf(mine),
    correct: pickOf(correct),
    locked,
    me,
    rival: rival && { player: rival.player, name: rival.name, picked: pickOf(rival.payload) },
  }
  const choice = (options: { id: string; text: string }[], mono?: boolean) => (
    <OptionList {...tiles} options={options} mono={mono} onPick={(id) => onAnswer({ optionId: id })} />
  )

  return (
    <div className="grid gap-5 [zoom:1.05]">
      <h2 className="font-heading text-xl leading-snug font-semibold text-balance sm:text-2xl">{promptOf(content, t)}</h2>
      {content.type === 'true_false' ? (
        <OptionList
          {...tiles}
          options={[
            { id: 'true', text: t.match.yes },
            { id: 'false', text: t.match.no },
          ]}
          trueFalse
          onPick={(id) => onAnswer({ value: id === 'true' })}
        />
      ) : content.type === 'multiple_choice' ? (
        <>
          {content.code && <CodeBlock code={content.code} language={language} />}
          {choice(content.options)}
        </>
      ) : content.type === 'cloze' || content.type === 'output' ? (
        <>
          <CodeBlock code={content.code} language={language} />
          {choice(content.options, true)}
        </>
      ) : content.type === 'odd_one_out' ? (
        <OptionList {...tiles} options={content.items} mono onPick={(id) => onAnswer({ itemId: id })} />
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
          language={language}
          fixes={content.fixes}
          mine={mine && 'line' in mine ? mine : undefined}
          correct={correct && 'line' in correct ? correct : undefined}
          locked={locked}
          me={me}
          rival={tiles.rival}
          onAnswer={onAnswer}
        />
      )}
    </div>
  )
}
