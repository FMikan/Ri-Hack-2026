import { ArrowLeft, BookOpen, Check, X } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router'
import { AiWorking } from '@/components/shared/AiWorking'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { QuestionBody } from '@/features/match/QuestionBody'
import { promptOf } from '@/features/match/question-utils'
import { useAsync } from '@/hooks/use-async'
import { api, ApiError } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { AnswerPayload, ProfessorQuizDto, ProfessorQuizResultDto } from '@/types/api'
import { Inline } from './Markdown'
import { useCreateStudy } from './use-create-study'

// A practice quiz the professor wrote from one answer: one question at a time, checked by the server as soon as it is
// answered, with the explanation right away. Nothing is scored; the first answer to each question stays.
export function ProfessorQuizPage() {
  const { id = '' } = useParams()
  const { t } = useLocale()
  const load = useCallback((signal: AbortSignal) => api.professorQuiz(id, signal), [id])
  const quiz = useAsync(load)

  return (
    <main className="mx-auto grid w-full max-w-3xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${quiz.data?.title ?? t.professor.quiz.documentTitle} · StackOff`}</title>
      {quiz.data ? (
        <QuizView key={quiz.data.id} quiz={quiz.data} />
      ) : quiz.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{t.professor.quiz.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{quiz.error.message}</p>
            <Button variant="outline" onClick={quiz.reload}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div aria-busy="true" className="grid gap-4">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          <Skeleton className="h-20 rounded-xl" />
          <Skeleton className="h-80 rounded-xl" />
        </div>
      )}
    </main>
  )
}

type Results = Record<number, ProfessorQuizResultDto>

function QuizView({ quiz }: { quiz: ProfessorQuizDto }) {
  const { t } = useLocale()
  const [results, setResults] = useState<Results>(() =>
    Object.fromEntries(quiz.questions.flatMap((q) => (q.result ? [[q.index, q.result]] : []))),
  )
  const firstOpen = quiz.questions.findIndex((q) => !q.result)
  const [index, setIndex] = useState(firstOpen === -1 ? quiz.questions.length : firstOpen) // past the end: the result
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string>()
  const question = quiz.questions[index]
  const result = results[index]
  const last = index === quiz.questions.length - 1

  async function answer(payload: AnswerPayload) {
    if (sending || result) return
    setSending(true)
    setError(undefined)
    try {
      const checked = await api.answerProfessorQuiz(quiz.id, { index, payload })
      setResults((previous) => ({ ...previous, [index]: checked }))
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 0 ? t.common.serverUnreachable : t.professor.quiz.answerFailed)
    } finally {
      setSending(false)
    }
  }

  return (
    <>
      <header className="grid justify-items-start gap-2">
        <Link
          to={`/professor?chat=${quiz.chatId}`}
          className="flex items-center gap-1.5 rounded-sm text-sm text-muted-foreground hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <ArrowLeft aria-hidden="true" className="size-4" />
          {t.professor.backToChat}
        </Link>
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-balance sm:text-3xl">{quiz.title}</h1>
        <p className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
          <Badge variant="secondary">{quiz.topic}</Badge>
          {t.professor.practiceOnly}
        </p>
      </header>
      {question ? (
        <section className="grid gap-4">
          <p className="font-mono text-xs text-muted-foreground">{t.lesson.questionOf(index + 1, quiz.questions.length)}</p>
          <Card>
            <CardContent>
              <QuestionBody
                content={question.content}
                language={question.language}
                mine={result?.payload}
                correct={result?.answer}
                locked={result !== undefined || sending}
                onAnswer={answer}
              />
            </CardContent>
          </Card>
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
          {result && (
            <>
              <div
                role="status"
                className={cn(
                  'grid gap-2 rounded-xl p-4 ring-1 ring-inset',
                  result.isCorrect ? 'bg-success/10 ring-success/40' : 'bg-destructive/10 ring-destructive/40',
                )}
              >
                <p className={cn('flex items-center gap-1.5 font-medium', result.isCorrect ? 'text-success' : 'text-destructive')}>
                  {result.isCorrect ? <Check aria-hidden="true" className="size-4" /> : <X aria-hidden="true" className="size-4" />}
                  {result.isCorrect ? t.lesson.correct : t.lesson.wrong}
                </p>
                <p className="text-sm text-pretty">
                  <Inline text={result.explanation} />
                </p>
              </div>
              <div className="flex justify-end">
                <Button size="lg" onClick={() => setIndex(index + 1)}>
                  {last ? t.professor.quiz.finish : t.professor.quiz.next}
                </Button>
              </div>
            </>
          )}
        </section>
      ) : (
        <Summary quiz={quiz} results={results} onReview={setIndex} />
      )}
    </>
  )
}

function Summary({ quiz, results, onReview }: { quiz: ProfessorQuizDto; results: Results; onReview: (index: number) => void }) {
  const { t } = useLocale()
  const study = useCreateStudy()
  const right = Object.values(results).filter((r) => r.isCorrect).length

  return (
    <section className="grid gap-5">
      <Card>
        <CardContent className="grid justify-items-start gap-4">
          <p className="font-heading text-3xl font-semibold tabular-nums">{t.professor.quiz.score(right, quiz.questions.length)}</p>
          <div className="flex flex-wrap gap-2">
            {quiz.lessonId ? (
              <Button asChild>
                <Link to={`/professor/lessons/${quiz.lessonId}`}>
                  <BookOpen aria-hidden="true" />
                  {t.professor.quiz.readLesson}
                </Link>
              </Button>
            ) : (
              <Button aria-disabled={study.making !== undefined} onClick={() => study.make('lesson', quiz.messageId, quiz.topic)}>
                <BookOpen aria-hidden="true" />
                {t.professor.quiz.makeLesson}
              </Button>
            )}
            <Button asChild variant="outline">
              <Link to={`/professor?chat=${quiz.chatId}`}>{t.professor.backToChat}</Link>
            </Button>
          </div>
          {study.error && (
            <p role="alert" className="text-sm text-destructive">
              {study.error.text}
            </p>
          )}
        </CardContent>
      </Card>
      <h2 className="font-heading text-lg font-semibold">{t.professor.quiz.review}</h2>
      <ol className="grid gap-3">
        {quiz.questions.map((question) => {
          const result = results[question.index]
          return (
            <li key={question.index}>
              <button
                type="button"
                onClick={() => onReview(question.index)}
                className="flex w-full items-start gap-3 rounded-xl bg-card p-4 text-left ring-1 ring-foreground/10 transition-colors hover:bg-secondary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                {result?.isCorrect ? (
                  <Check aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-success" />
                ) : (
                  <X aria-hidden="true" className="mt-0.5 size-5 shrink-0 text-destructive" />
                )}
                <span className="grid min-w-0 gap-1">
                  <span className="text-xs text-muted-foreground">
                    {t.professor.quiz.reviewQuestion(question.index + 1)} · {result?.isCorrect ? t.lesson.correct : t.lesson.wrong}
                  </span>
                  <span className="font-medium text-pretty">{promptOf(question.content, t)}</span>
                  {result && (
                    <span className="text-sm text-pretty text-muted-foreground">
                      <Inline text={result.explanation} />
                    </span>
                  )}
                </span>
              </button>
            </li>
          )
        })}
      </ol>
      <AiWorking open={study.making !== undefined} title={t.professor.makingLesson(quiz.topic)} description={t.professor.makingHint} />
    </section>
  )
}
