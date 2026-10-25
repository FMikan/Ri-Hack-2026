import { ArrowLeft, BookOpen, ChevronDown, Flag, Sparkles } from 'lucide-react'
import { useCallback, useState, type SubmitEvent } from 'react'
import { Link, useParams } from 'react-router'
import { AiWorking } from '@/components/shared/AiWorking'
import { DeltaBadge } from '@/components/shared/DeltaBadge'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { useStartLesson } from '@/features/lesson/use-start-lesson'
import { AnswerChip } from '@/features/match/AnswerChip'
import { Explanation } from '@/features/match/Explanation'
import { QuestionBody } from '@/features/match/QuestionBody'
import { promptOf } from '@/features/match/question-utils'
import { useAsync } from '@/hooks/use-async'
import { api, ApiError } from '@/lib/api-client'
import { relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import type { MatchReviewDto, ReviewQuestionDto } from '@/types/api'

// After a match: every question with both answers and the key, the AI explanation on demand, how mastery moved, and
// the lesson for what was missed (game idea §10–11). Misses start open.
export function ReviewPage() {
  const { id = '' } = useParams()
  const { t } = useLocale()
  const load = useCallback((signal: AbortSignal) => api.review(id, signal), [id])
  const review = useAsync(load)

  // grid-cols-1 (minmax(0, 1fr)) on main, the questions section and its list: an auto column grows to fit a long
  // one-line (truncated) prompt in a question's summary, and the page scrolls sideways.
  return (
    <main className="mx-auto grid w-full max-w-3xl grid-cols-1 content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${t.review.documentTitle} · StackOff`}</title>
      <Link
        to="/profile"
        className="flex w-fit items-center gap-1.5 rounded-sm text-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <ArrowLeft aria-hidden="true" className="size-4" />
        {t.review.back}
      </Link>
      {review.data ? (
        <ReviewView review={review.data} />
      ) : review.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{t.review.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{review.error.message}</p>
            <Button variant="outline" onClick={review.reload}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div aria-busy="true" className="grid gap-4">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          <Skeleton className="h-28 rounded-xl" />
          <Skeleton className="h-96 rounded-xl" />
        </div>
      )}
    </main>
  )
}

function ReviewView({ review }: { review: MatchReviewDto }) {
  const { t, locale } = useLocale()
  const copy = t.review
  const { match } = review
  const opponentName = !match.opponent
    ? undefined
    : match.opponent.persona
      ? t.bots[match.opponent.persona]
      : (match.opponent.user?.username ?? '')
  const missed = review.questions.filter((question) => !question.mine.isCorrect).length
  const lesson = useStartLesson(match.id, match.lessonId)

  return (
    <>
      <header className="grid gap-2">
        <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{copy.title}</h1>
        <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
          {[match.topic?.name, opponentName && copy.versus(opponentName), match.finishedAt && relativeTime(match.finishedAt, locale)]
            .filter(Boolean)
            .join(' · ')}
        </p>
        <p className="flex flex-wrap items-center gap-3">
          <span className="font-heading text-3xl font-semibold tabular-nums">
            {match.opponent ? `${match.myScore} : ${match.opponentScore}` : match.myScore}
          </span>
          <Badge variant="secondary">{t.profile.history.outcomes[match.outcome]}</Badge>
          <DeltaBadge value={match.ratingDelta ?? 0} />
        </p>
      </header>

      {missed > 0 && (
        <section className="flex flex-wrap items-center gap-3 rounded-xl bg-primary/10 p-4 ring-1 ring-primary/40 ring-inset">
          <BookOpen aria-hidden="true" className="size-5 shrink-0 text-primary" />
          <p className="min-w-0 flex-1 text-sm text-pretty">{copy.lessonPrompt(missed)}</p>
          {lesson.error && (
            <p role="alert" className="text-sm text-destructive">
              {lesson.error}
            </p>
          )}
          <Button aria-disabled={lesson.starting} onClick={lesson.start}>
            {match.lessonId ? copy.openLesson : copy.startLesson}
          </Button>
          <AiWorking open={lesson.starting} title={t.lesson.preparing} description={t.lesson.preparingHint} />
        </section>
      )}

      {review.masteryDelta.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>
              <h2>{copy.mastery}</h2>
            </CardTitle>
            <CardDescription>{copy.masteryHint}</CardDescription>
          </CardHeader>
          <CardContent>
            <ul className="grid gap-2 sm:grid-cols-2">
              {review.masteryDelta.map(({ concept, before, after }) => (
                <li key={concept.id} className="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm ring-1 ring-border ring-inset">
                  <span className="min-w-0 flex-1 truncate font-medium">{concept.name}</span>
                  <span className="font-mono text-xs text-muted-foreground tabular-nums">
                    {Math.round(before)} → {Math.round(after)}
                  </span>
                  <DeltaBadge value={Math.round(after - before)} />
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

      <section className="grid grid-cols-1 gap-3">
        <h2 className="font-heading text-lg font-semibold">{copy.questions}</h2>
        <ol className="grid grid-cols-1 gap-2">
          {review.questions.map((question) => (
            <ReviewItem key={question.index} question={question} opponentName={opponentName} />
          ))}
        </ol>
      </section>
    </>
  )
}

function ReviewItem({ question, opponentName }: { question: ReviewQuestionDto; opponentName?: string }) {
  const { t } = useLocale()
  const copy = t.review
  const [explain, setExplain] = useState(!!question.explanation)

  return (
    <li>
      <details open={!question.mine.isCorrect} className="group rounded-xl bg-card ring-1 ring-foreground/10">
        <summary className="flex cursor-pointer list-none items-center gap-3 rounded-xl p-3 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring sm:p-4 [&::-webkit-details-marker]:hidden">
          <span className="w-5 shrink-0 font-mono text-xs text-muted-foreground">{question.index + 1}</span>
          <span className="min-w-0 flex-1 truncate text-sm font-medium">{promptOf(question.question.content, t)}</span>
          <AnswerChip result={question.mine} />
          <ChevronDown aria-hidden="true" className="size-4 shrink-0 text-muted-foreground transition-transform group-open:rotate-180 motion-reduce:transition-none" />
        </summary>
        <div className="grid gap-4 border-t border-border p-4">
          <p className="flex flex-wrap gap-2 text-xs">
            <Badge variant="secondary">{question.concept.name}</Badge>
            <Badge variant="outline">{copy.slots[question.slot]}</Badge>
          </p>
          <QuestionBody content={question.question.content} language={question.question.language} mine={question.mine.payload} correct={question.key.answer} locked onAnswer={() => {}} />
          <div className="flex flex-wrap gap-2">
            <AnswerChip who={t.match.you} result={question.mine} />
            {question.opponent && opponentName && <AnswerChip who={opponentName} result={question.opponent} />}
          </div>
          <p className="text-sm text-pretty">
            <span className="font-medium text-primary">{t.match.remember}:</span> {question.key.remember}
          </p>
          {explain ? (
            <Explanation questionId={question.question.id} initial={question.explanation} showUnavailable />
          ) : (
            <Button variant="outline" size="sm" className="justify-self-start" onClick={() => setExplain(true)}>
              <Sparkles aria-hidden="true" />
              {copy.explain}
            </Button>
          )}
          <ReportQuestion questionId={question.question.id} reported={question.reported} />
        </div>
      </details>
    </li>
  )
}

// Three reports take a question out of new decks (docs §6.4); one per player.
function ReportQuestion({ questionId, reported }: { questionId: string; reported: boolean }) {
  const { t } = useLocale()
  const copy = t.review.report
  const [open, setOpen] = useState(false)
  const [done, setDone] = useState(reported)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string>()
  const fieldId = `report-${questionId}`

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    const reason = String(new FormData(event.currentTarget).get('reason') ?? '').trim()
    if (!reason) return setError(copy.empty)
    setSending(true)
    setError(undefined)
    try {
      await api.reportQuestion(questionId, { reason })
      setDone(true)
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 0 ? t.common.serverUnreachable : String((caught as Error).message))
    } finally {
      setSending(false)
    }
  }

  if (done)
    return (
      <p role="status" className="flex items-center gap-1.5 text-xs text-muted-foreground">
        <Flag aria-hidden="true" className="size-3.5" />
        {copy.done}
      </p>
    )
  if (!open)
    return (
      <Button variant="ghost" size="sm" className="justify-self-start text-muted-foreground" onClick={() => setOpen(true)}>
        <Flag aria-hidden="true" />
        {copy.open}
      </Button>
    )
  return (
    <form className="grid gap-2" onSubmit={submit}>
      <Label htmlFor={fieldId}>{copy.label}</Label>
      <Textarea id={fieldId} name="reason" maxLength={500} rows={3} autoFocus aria-invalid={!!error} />
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      <div className="flex flex-wrap gap-2">
        <Button type="submit" size="sm" aria-disabled={sending}>
          {sending ? copy.sending : copy.send}
        </Button>
        <Button type="button" variant="ghost" size="sm" onClick={() => setOpen(false)}>
          {copy.cancel}
        </Button>
      </div>
    </form>
  )
}
