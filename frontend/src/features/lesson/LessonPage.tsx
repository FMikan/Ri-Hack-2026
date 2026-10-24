import { ArrowLeft, Check, CircleAlert, X } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { Rewards } from '@/components/shared/Rewards'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { CodeBlock } from '@/features/match/CodeBlock'
import { QuestionBody } from '@/features/match/QuestionBody'
import { useAsync } from '@/hooks/use-async'
import { api, ApiError } from '@/lib/api-client'
import { relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { AnswerPayload, LessonCheckResultDto, LessonDto, LessonResultDto } from '@/types/api'

// The lesson after a duel (game idea §11): one card per missed concept, then three untimed check questions.
// Passing patches the concepts (they enter the review queue); a miss reopens the check with new questions in 10 min.
export function LessonPage() {
  const { id = '' } = useParams()
  const { t } = useLocale()
  const load = useCallback((signal: AbortSignal) => api.lesson(id, signal), [id])
  const lesson = useAsync(load)

  return (
    <main className="mx-auto grid w-full max-w-3xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${t.lesson.documentTitle} · StackOff`}</title>
      <header className="grid gap-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{t.lesson.title}</h1>
        {lesson.data && <p className="text-sm text-muted-foreground">{lesson.data.concepts.map((concept) => concept.name).join(' · ')}</p>}
      </header>
      {lesson.data ? (
        <LessonView key={`${lesson.data.attempt}-${lesson.data.status}`} lesson={lesson.data} reload={lesson.reload} />
      ) : lesson.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{t.lesson.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{lesson.error.message}</p>
            <Button variant="outline" onClick={lesson.reload}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div aria-busy="true" className="grid gap-4">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          <Skeleton className="h-72 rounded-xl" />
        </div>
      )}
    </main>
  )
}

type Stage = 'cards' | 'check'

function LessonView({ lesson, reload }: { lesson: LessonDto; reload: () => void }) {
  const [stage, setStage] = useState<Stage>('cards')
  const [result, setResult] = useState<{ result: LessonResultDto; answers: Record<string, AnswerPayload> }>()

  if (result) return <CheckResult lesson={lesson} {...result} onCards={reload} />
  if (lesson.status === 'passed') return <Passed lesson={lesson} />
  if (lesson.status === 'failed' && stage === 'cards' && lesson.lastResults)
    return <Failed lesson={lesson} onRetry={() => setStage('check')} />
  return stage === 'cards' ? (
    <Cards lesson={lesson} onStart={() => setStage('check')} />
  ) : (
    <CheckQuestions lesson={lesson} onBack={() => setStage('cards')} onResult={(next) => setResult(next)} />
  )
}

function Cards({ lesson, onStart }: { lesson: LessonDto; onStart: () => void }) {
  const { t } = useLocale()
  const copy = t.lesson
  const [index, setIndex] = useState(0)
  const card = lesson.cards[index]
  const last = index === lesson.cards.length - 1
  const concept = lesson.concepts.find((entry) => entry.id === card?.conceptId)

  if (!card) return <Button onClick={onStart}>{copy.startCheck(lesson.check.length)}</Button>

  return (
    <section className="grid gap-4">
      <Card>
        <CardContent className="grid gap-4">
          <p className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            <span className="font-mono">{copy.cardOf(index + 1, lesson.cards.length)}</span>
            {concept && <Badge variant="secondary">{concept.name}</Badge>}
          </p>
          <h2 className="font-heading text-xl font-semibold text-balance sm:text-2xl">{card.title}</h2>
          <p className="text-pretty">{card.why}</p>
          {card.code && <CodeBlock code={card.code} />}
          {card.misconception && (
            <div className="grid gap-1 rounded-lg bg-warning/10 p-3 ring-1 ring-warning/40 ring-inset">
              <p className="flex items-center gap-1.5 text-xs font-medium text-warning">
                <CircleAlert aria-hidden="true" className="size-3.5" />
                {copy.misconception}
              </p>
              <p className="text-sm text-pretty">{card.misconception}</p>
            </div>
          )}
        </CardContent>
      </Card>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Button variant="outline" className={cn(index === 0 && 'invisible')} onClick={() => setIndex(index - 1)}>
          <ArrowLeft aria-hidden="true" />
          {copy.previous}
        </Button>
        {last ? (
          <Button size="lg" onClick={onStart}>
            {copy.startCheck(lesson.check.length)}
          </Button>
        ) : (
          <Button size="lg" onClick={() => setIndex(index + 1)}>
            {copy.next}
          </Button>
        )}
      </div>
    </section>
  )
}

interface CheckQuestionsProps {
  lesson: LessonDto
  onBack: () => void
  onResult: (result: { result: LessonResultDto; answers: Record<string, AnswerPayload> }) => void
}

// Untimed, one question at a time; the answers go to the server together.
function CheckQuestions({ lesson, onBack, onResult }: CheckQuestionsProps) {
  const { t } = useLocale()
  const copy = t.lesson
  const [index, setIndex] = useState(0)
  const [answers, setAnswers] = useState<Record<string, AnswerPayload>>({})
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string>()
  const item = lesson.check[index]
  const answer = answers[item.question.id]
  const last = index === lesson.check.length - 1

  async function submit() {
    setSending(true)
    setError(undefined)
    try {
      const result = await api.checkLesson(lesson.id, {
        answers: lesson.check.map(({ question }) => ({ questionId: question.id, payload: answers[question.id] })),
      })
      onResult({ result, answers })
    } catch (caught) {
      setError(
        caught instanceof ApiError && caught.code === 'urn:app:lesson/retry-too-soon'
          ? copy.tooSoon
          : caught instanceof ApiError && caught.status === 0
            ? t.common.serverUnreachable
            : String((caught as Error).message),
      )
      setSending(false)
    }
  }

  return (
    <section className="grid gap-4">
      <p className="font-mono text-xs text-muted-foreground">{copy.questionOf(index + 1, lesson.check.length)}</p>
      <Card>
        <CardContent>
          <QuestionBody
            key={item.question.id}
            content={item.question.content}
            mine={answer}
            locked={!!answer}
            onAnswer={(payload) => setAnswers((previous) => ({ ...previous, [item.question.id]: payload }))}
          />
        </CardContent>
      </Card>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Button variant="outline" onClick={() => (index === 0 ? onBack() : setIndex(index - 1))}>
          <ArrowLeft aria-hidden="true" />
          {index === 0 ? copy.backToCards : copy.previous}
        </Button>
        {last ? (
          <Button size="lg" aria-disabled={!answer || sending} onClick={() => answer && !sending && submit()}>
            {sending ? copy.checking : copy.submit}
          </Button>
        ) : (
          <Button size="lg" aria-disabled={!answer} onClick={() => answer && setIndex(index + 1)}>
            {copy.next}
          </Button>
        )}
      </div>
    </section>
  )
}

interface CheckResultProps {
  lesson: LessonDto
  result: LessonResultDto
  answers: Record<string, AnswerPayload>
  onCards: () => void
}

function CheckResult({ lesson, result, answers, onCards }: CheckResultProps) {
  const { t } = useLocale()
  const copy = t.lesson

  return (
    <section className="grid gap-5">
      <Outcome passed={result.passed} />
      {result.passed ? (
        <>
          <Patched names={result.patchedConcepts.map((concept) => concept.name)} />
          <Rewards coins={result.rewards.coins} xp={result.rewards.xp} sp={result.rewards.sp} />
        </>
      ) : (
        <p className="text-center text-sm text-pretty text-muted-foreground">{copy.failedHint}</p>
      )}
      <ol className="grid gap-3">
        {lesson.check.map(({ question }, index) => {
          const graded = result.results.find((entry) => entry.questionId === question.id)
          return (
            <li key={question.id}>
              <Card>
                <CardContent className="grid gap-3">
                  <p className="font-mono text-xs text-muted-foreground">{copy.questionOf(index + 1, lesson.check.length)}</p>
                  <QuestionBody
                    content={question.content}
                    mine={answers[question.id]}
                    correct={graded?.key.answer}
                    locked
                    onAnswer={() => {}}
                  />
                  {graded && <Remember result={graded} />}
                </CardContent>
              </Card>
            </li>
          )
        })}
      </ol>
      <Actions lesson={lesson} passed={result.passed} onCards={onCards} />
    </section>
  )
}

function Passed({ lesson }: { lesson: LessonDto }) {
  return (
    <section className="grid gap-5">
      <Outcome passed />
      <Patched names={lesson.concepts.map((concept) => concept.name)} />
      {lesson.lastResults && <PastResults results={lesson.lastResults} />}
      <Actions lesson={lesson} passed onCards={() => {}} />
    </section>
  )
}

// After a miss: the last answers with their keys, and the check again once retryAfter has passed.
function Failed({ lesson, onRetry }: { lesson: LessonDto; onRetry: () => void }) {
  const { t, locale } = useLocale()
  const copy = t.lesson
  // Only a hint for when to come back; the server answers 409 retry-too-soon if it is still early.
  const [open, setOpen] = useState(false)
  useEffect(() => {
    const wait = lesson.retryAfter ? Date.parse(lesson.retryAfter) - Date.now() : 0
    const id = window.setTimeout(() => setOpen(true), Math.max(0, wait))
    return () => window.clearTimeout(id)
  }, [lesson.retryAfter])

  return (
    <section className="grid gap-5">
      <Outcome passed={false} />
      <p className="text-center text-sm text-pretty text-muted-foreground">
        {open ? copy.retryOpen : copy.retryAt(relativeTime(lesson.retryAfter!, locale))}
      </p>
      {lesson.lastResults && <PastResults results={lesson.lastResults} />}
      <Cards lesson={lesson} onStart={onRetry} />
    </section>
  )
}

function Outcome({ passed }: { passed: boolean }) {
  const { t } = useLocale()
  return (
    <header aria-live="polite" className="grid justify-items-center gap-2 pt-4 text-center">
      <span
        className={cn(
          'grid size-14 place-items-center rounded-full',
          passed ? 'bg-success/15 text-success' : 'bg-destructive/15 text-destructive',
        )}
      >
        {passed ? <Check aria-hidden="true" className="size-7" /> : <X aria-hidden="true" className="size-7" />}
      </span>
      <h2 className="font-heading text-3xl font-semibold tracking-tight">{passed ? t.lesson.passed : t.lesson.failed}</h2>
    </header>
  )
}

function Patched({ names }: { names: string[] }) {
  const { t } = useLocale()
  return (
    <div className="grid justify-items-center gap-2 text-center">
      <p className="text-sm text-muted-foreground">{t.lesson.patched}</p>
      <ul className="flex flex-wrap justify-center gap-2">
        {names.map((name) => (
          <li key={name}>
            <Badge variant="success">{name}</Badge>
          </li>
        ))}
      </ul>
    </div>
  )
}

function Remember({ result }: { result: LessonCheckResultDto }) {
  const { t } = useLocale()
  return (
    <p className="flex items-start gap-2 text-sm text-pretty">
      {result.isCorrect ? (
        <Check aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-success" />
      ) : (
        <X aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-destructive" />
      )}
      <span>
        <span className="sr-only">{result.isCorrect ? t.lesson.correct : t.lesson.wrong}. </span>
        <span className="font-medium text-primary">{t.match.remember}:</span> {result.key.remember}
      </span>
    </p>
  )
}

function PastResults({ results }: { results: LessonCheckResultDto[] }) {
  const { t } = useLocale()
  return (
    <Card>
      <CardContent className="grid gap-3">
        <h3 className="text-sm font-medium text-muted-foreground">{t.lesson.lastCheck}</h3>
        <ul className="grid gap-2">
          {results.map((result) => (
            <li key={result.questionId}>
              <Remember result={result} />
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  )
}

function Actions({ lesson, passed, onCards }: { lesson: LessonDto; passed: boolean; onCards: () => void }) {
  const { t } = useLocale()
  const copy = t.lesson
  return (
    <div className="flex flex-wrap justify-center gap-3 pt-2">
      {!passed && (
        <Button size="lg" onClick={onCards}>
          {copy.readAgain}
        </Button>
      )}
      {passed && (
        <Button asChild size="lg">
          <Link to="/practice">{copy.practice}</Link>
        </Button>
      )}
      {lesson.matchId && (
        <Button asChild size="lg" variant="outline">
          <Link to={`/match/${lesson.matchId}/review`}>{copy.review}</Link>
        </Button>
      )}
      <Button asChild size="lg" variant="outline">
        <Link to="/">{t.match.result.dashboard}</Link>
      </Button>
    </div>
  )
}
