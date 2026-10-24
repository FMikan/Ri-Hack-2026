import { Target } from 'lucide-react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { QUESTIONS_PER_MATCH } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { MatchStateDto, PlayerDto, ReviewQuestionDto } from '@/types/api'
import { AnswerChip } from './AnswerChip'
import { promptOf } from './question-utils'

interface ResultViewProps {
  match: MatchStateDto
  me?: PlayerDto
  opponent?: PlayerDto
  opponentName: string
}

// Outcome, final score and the three cards from the game idea (§10). Rating, coins and XP stay 0 until F3/F4,
// so they are not shown yet.
export function ResultView({ match, me, opponent, opponentName }: ResultViewProps) {
  const { t } = useLocale()
  const copy = t.match.result
  const result = match.result!
  const outcome = !opponent
    ? 'completed'
    : match.status === 'aborted'
      ? 'aborted'
      : result.winnerSeat == null
        ? 'draw'
        : result.winnerSeat === match.mySeat
          ? 'win'
          : 'loss'
  const { blunder, brilliant, practiceConcept } = result.cards

  const side = (name: string, score: number, correct: number | undefined, player?: PlayerDto) => (
    <div className="grid justify-items-center gap-1 text-center">
      <p className="max-w-full truncate text-sm font-medium">{name}</p>
      <p className="font-heading text-4xl font-semibold tabular-nums sm:text-5xl">{score}</p>
      {correct !== undefined && <p className="text-xs text-muted-foreground">{copy.correct(correct, match.question?.total ?? QUESTIONS_PER_MATCH)}</p>}
      {opponent && <p className="font-mono text-xs text-muted-foreground">{player?.displayRating}</p>}
    </div>
  )

  return (
    <section className="grid gap-5">
      <header className="grid justify-items-center gap-2 pt-6 text-center">
        <p className="text-sm text-muted-foreground">{match.topic?.name}</p>
        <h2 className={cn('font-heading text-4xl font-semibold tracking-tight sm:text-5xl', outcome === 'win' && 'text-success')}>
          {copy[outcome]}
        </h2>
        {outcome !== 'completed' && <p className="text-sm text-muted-foreground">{copy.reasons[result.reason]}</p>}
      </header>

      <Card>
        {opponent ? (
          <CardContent className="grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-4">
            {side(t.match.you, result.mine.score, result.mine.correct, me)}
            <span className="font-mono text-sm text-muted-foreground">vs</span>
            {result.opponent && side(opponentName, result.opponent.score, result.opponent.correct, opponent)}
          </CardContent>
        ) : (
          <CardContent>{side(t.match.you, result.mine.score, result.mine.correct, me)}</CardContent>
        )}
      </Card>

      {(blunder || brilliant || practiceConcept) && (
        <ul className="grid gap-3 sm:grid-cols-3">
          {blunder && <QuestionCard title={copy.cards.blunder} card={blunder} />}
          {brilliant && <QuestionCard title={copy.cards.brilliant} card={brilliant} />}
          {practiceConcept && (
            <li className="grid content-start gap-2 rounded-xl bg-card p-4 ring-1 ring-foreground/10">
              <p className="text-xs text-muted-foreground">{copy.cards.practice}</p>
              <p className="flex items-center gap-2 font-medium">
                <Target aria-hidden="true" className="size-4 shrink-0 text-primary" />
                {practiceConcept.concept.name}
              </p>
              <p className={cn('font-mono text-xs', practiceConcept.masteryDelta < 0 ? 'text-destructive' : 'text-success')}>
                {copy.masteryDelta(practiceConcept.masteryDelta)}
              </p>
            </li>
          )}
        </ul>
      )}

      <div className="flex flex-wrap justify-center gap-3 pt-2">
        <Button asChild size="lg">
          {opponent ? <Link to="/play">{copy.playAgain}</Link> : <Link to="/practice">{copy.practiceAgain}</Link>}
        </Button>
        <Button asChild size="lg" variant="outline">
          <Link to="/">{copy.dashboard}</Link>
        </Button>
      </div>
    </section>
  )
}

function QuestionCard({ title, card }: { title: string; card: ReviewQuestionDto }) {
  const { t } = useLocale()

  return (
    <li className="grid content-start gap-2 rounded-xl bg-card p-4 ring-1 ring-foreground/10">
      <p className="text-xs text-muted-foreground">{title}</p>
      <p className="font-medium">{card.concept.name}</p>
      <p className="line-clamp-3 text-sm text-pretty text-muted-foreground">{promptOf(card.question.content, t)}</p>
      <span className="pt-1">
        <AnswerChip result={card.mine} />
      </span>
    </li>
  )
}
