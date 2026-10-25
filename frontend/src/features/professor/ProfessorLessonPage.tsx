import { ArrowLeft, ListChecks } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router'
import { AiWorking } from '@/components/shared/AiWorking'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { StudyCard } from '@/features/lesson/StudyCard'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { ProfessorLessonDto } from '@/types/api'
import { Inline } from './Markdown'
import { useCreateStudy } from './use-create-study'

// A lesson the professor wrote from one answer: cards one at a time, then a practice quiz on the same answer.
export function ProfessorLessonPage() {
  const { id = '' } = useParams()
  const { t } = useLocale()
  const load = useCallback((signal: AbortSignal) => api.professorLesson(id, signal), [id])
  const lesson = useAsync(load)

  return (
    <main className="mx-auto grid w-full max-w-3xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${lesson.data?.title ?? t.professor.lesson.documentTitle} · StackOff`}</title>
      {lesson.data ? (
        <LessonView key={lesson.data.id} lesson={lesson.data} />
      ) : lesson.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{t.professor.lesson.loadFailed}</p>
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
          <Skeleton className="h-20 rounded-xl" />
          <Skeleton className="h-72 rounded-xl" />
        </div>
      )}
    </main>
  )
}

function LessonView({ lesson }: { lesson: ProfessorLessonDto }) {
  const { t } = useLocale()
  const [index, setIndex] = useState(0)
  const study = useCreateStudy()
  const card = lesson.cards[index]
  const last = index === lesson.cards.length - 1

  return (
    <>
      <header className="grid justify-items-start gap-2">
        <Link
          to={`/professor?chat=${lesson.chatId}`}
          className="flex items-center gap-1.5 rounded-sm text-sm text-muted-foreground hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <ArrowLeft aria-hidden="true" className="size-4" />
          {t.professor.backToChat}
        </Link>
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-balance sm:text-3xl">{lesson.title}</h1>
        {lesson.summary && <p className="text-pretty text-muted-foreground">{lesson.summary}</p>}
        <p className="text-xs text-muted-foreground">{t.professor.practiceOnly}</p>
      </header>
      <StudyCard
        meta={
          <>
            <span className="font-mono">{t.lesson.cardOf(index + 1, lesson.cards.length)}</span>
            <Badge variant="secondary">{lesson.topic}</Badge>
          </>
        }
        title={card.title}
        body={<Inline text={card.body} />}
        code={card.code}
        language={card.language}
        misconception={card.misconception}
      />
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Button variant="outline" className={cn(index === 0 && 'invisible')} onClick={() => setIndex(index - 1)}>
          <ArrowLeft aria-hidden="true" />
          {t.lesson.previous}
        </Button>
        {!last ? (
          <Button size="lg" onClick={() => setIndex(index + 1)}>
            {t.lesson.next}
          </Button>
        ) : lesson.quizId ? (
          <Button asChild size="lg">
            <Link to={`/professor/quizzes/${lesson.quizId}`}>
              <ListChecks aria-hidden="true" />
              {t.professor.lesson.openQuiz}
            </Link>
          </Button>
        ) : (
          <Button size="lg" aria-disabled={study.making !== undefined} onClick={() => study.make('quiz', lesson.messageId, lesson.topic)}>
            <ListChecks aria-hidden="true" />
            {t.professor.lesson.quizMe}
          </Button>
        )}
      </div>
      {study.error && (
        <p role="alert" className="text-sm text-destructive">
          {study.error.text}
        </p>
      )}
      <AiWorking open={study.making !== undefined} title={t.professor.makingQuiz(lesson.topic)} description={t.professor.makingHint} />
    </>
  )
}
