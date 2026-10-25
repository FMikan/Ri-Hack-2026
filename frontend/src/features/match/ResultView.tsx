import { Award, BookOpen, Flame, Target } from 'lucide-react'
import { useEffect } from 'react'
import { Link } from 'react-router'
import { DeltaBadge } from '@/components/shared/DeltaBadge'
import { Rewards } from '@/components/shared/Rewards'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { useAuth } from '@/features/auth/auth-context'
import { useStartLesson } from '@/features/lesson/use-start-lesson'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { QUESTIONS_PER_MATCH, ratingText } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { MatchKind, MatchStateDto, PlayerDto, ReviewQuestionDto } from '@/types/api'
import { AnswerChip } from './AnswerChip'
import { PlayerAvatar } from './PlayerAvatar'
import { promptOf } from './question-utils'
import { RematchPanel } from './RematchPanel'

interface ResultViewProps {
  match: MatchStateDto
  me?: PlayerDto
  opponent?: PlayerDto
  opponentName: string
}

// Where "Play again" goes for each kind of duel; practice has its own button.
const AGAIN: Partial<Record<MatchKind, string>> = { ranked: '/?play=ranked', ghost: '/?play=ranked', bot: '/?play=bot', friend: '/friends' }

// Outcome, final score, rating change, rewards and the three cards from the game idea (§10).
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
  const { ratingBefore, ratingAfter } = result.mine
  const again = AGAIN[match.kind]
  const { refreshUser } = useAuth()
  // Coins, XP and the rating just changed on the server; the top bar reads them from the session.
  useEffect(() => {
    refreshUser().catch(() => {})
  }, [refreshUser])

  const side = (name: string, score: number, correct: number | undefined, player?: PlayerDto) => (
    <div className="grid justify-items-center gap-1 text-center">
      <PlayerAvatar player={player} className="mb-1 size-14" />
      <p className="max-w-full truncate text-sm font-medium">{name}</p>
      <p className="font-heading text-4xl font-semibold tabular-nums sm:text-5xl">{score}</p>
      {correct !== undefined && <p className="text-xs text-muted-foreground">{copy.correct(correct, match.question?.total ?? QUESTIONS_PER_MATCH)}</p>}
      {opponent && player && (
        <p className="font-mono text-xs text-muted-foreground">{ratingText(player.displayRating, player.user?.isProvisional)}</p>
      )}
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
        {ratingBefore != null && ratingAfter != null && (
          <p className="flex flex-wrap items-center justify-center gap-2 text-sm">
            {me?.user?.isProvisional ? (
              <span className="text-muted-foreground">{copy.placement}</span>
            ) : (
              <>
                <span className="text-muted-foreground">{copy.rating}</span>
                <span className="font-mono font-medium">{ratingAfter}</span>
              </>
            )}
            <DeltaBadge value={ratingAfter - ratingBefore} />
          </p>
        )}
        <Rewards coins={result.mine.coins} xp={result.mine.xp} sp={result.mine.sp} className="pt-1" />
        {result.mine.streakSecured && (
          <p className="flex items-center gap-1.5 text-sm text-warning">
            <Flame aria-hidden="true" className="size-4" />
            {copy.streakSecured}
          </p>
        )}
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

      <Unlocked match={match} />
      {result.rematchAvailable && opponent && <RematchPanel match={match} opponentName={opponentName} />}

      <div className="flex flex-wrap justify-center gap-3 pt-2">
        {/* Every loss ends with a short lesson (game idea §11), so it leads when there is one. */}
        {result.lessonAvailable && <LessonButton match={match} />}
        {result.reviewAvailable && (
          <Button asChild size="lg" variant="outline">
            <Link to={`/match/${match.id}/review`}>{copy.review}</Link>
          </Button>
        )}
        {!opponent ? (
          <Button asChild size="lg" variant={result.lessonAvailable ? 'outline' : 'default'}>
            <Link to="/practice">{copy.practiceAgain}</Link>
          </Button>
        ) : (
          again && (
            <Button asChild size="lg" variant={result.lessonAvailable ? 'outline' : 'default'}>
              <Link to={again}>{copy.playAgain}</Link>
            </Button>
          )
        )}
        <Button asChild size="lg" variant="outline">
          <Link to="/">{copy.dashboard}</Link>
        </Button>
      </div>
    </section>
  )
}

// Loading the list records any new achievement (docs §6.11 F5b); the ones earned during this duel are shown here.
const DUEL_WINDOW_MS = 15 * 60_000

function Unlocked({ match }: { match: MatchStateDto }) {
  const { t } = useLocale()
  const { data } = useAsync(api.achievements)
  const since = Date.parse(match.serverTime) - DUEL_WINDOW_MS
  const fresh = data?.filter((achievement) => achievement.earnedAt && Date.parse(achievement.earnedAt) >= since) ?? []
  if (!fresh.length) return null

  return (
    <ul aria-live="polite" className="flex flex-wrap justify-center gap-2">
      {fresh.map((achievement) => (
        <li key={achievement.key} className="flex items-center gap-2 rounded-full bg-warning/15 px-3 py-1.5 text-sm font-medium text-warning">
          <Award aria-hidden="true" className="size-4" />
          {t.match.result.unlocked(t.achievements[achievement.key as keyof typeof t.achievements] ?? achievement.name)}
        </li>
      ))}
    </ul>
  )
}

function LessonButton({ match }: { match: MatchStateDto }) {
  const { t } = useLocale()
  const result = match.result!
  const lesson = useStartLesson(match.id, result.lessonId)

  return (
    <span className="grid justify-items-center gap-1">
      <Button size="lg" aria-disabled={lesson.starting} onClick={lesson.start}>
        <BookOpen aria-hidden="true" />
        {result.lessonId ? t.match.result.openLesson : t.match.result.lesson}
        {result.lessonRewardEligible && !result.lessonId && <span className="font-mono text-xs opacity-80">{t.match.result.lessonReward}</span>}
      </Button>
      {lesson.error && (
        <span role="alert" className="text-xs text-destructive">
          {lesson.error}
        </span>
      )}
    </span>
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
