import { Check, Crosshair, Star, Target, X } from 'lucide-react'
import { useCallback, useState } from 'react'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { QuestionBody } from '@/features/match/QuestionBody'
import { useStartMatch } from '@/features/match/use-start-match'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { ConceptState, KnowledgeMapDto } from '@/types/api'

type TopicMap = KnowledgeMapDto['topics'][number]
type ConceptNode = TopicMap['concepts'][number]

// Colors follow the game idea (§11): gray unknown, red weak, yellow in progress, green solid. Graduated is sky with a
// star instead of gold, so it never reads as "in progress". Tiles stay neutral like the rest of the app; the color
// sits in the dot, next to the state in words.
const STATES: Record<ConceptState, { dot: string }> = {
  unknown: { dot: 'bg-muted-foreground/50' },
  weak: { dot: 'bg-destructive' },
  in_progress: { dot: 'bg-warning' },
  solid: { dot: 'bg-success' },
  graduated: { dot: 'bg-primary' },
}
const ORDER: ConceptState[] = ['unknown', 'weak', 'in_progress', 'solid', 'graduated']

// One topic at a time: a tile per concept; tapping one shows its last answers and "Practice this".
export function KnowledgeMapCard({ map, className }: { map: KnowledgeMapDto; className?: string }) {
  const { t } = useLocale()
  const copy = t.profile.map
  const [topicId, setTopicId] = useState(map.topics[0]?.topic.id)
  const [conceptId, setConceptId] = useState<string>()
  const { start, starting, error } = useStartMatch()
  const topic = map.topics.find((entry) => entry.topic.id === topicId) ?? map.topics[0]
  const selected = topic?.concepts.find((node) => node.concept.id === conceptId)

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>{copy.subtitle}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-5">
        {!topic ? (
          <p className="text-sm text-muted-foreground">{t.dashboard.skills.empty}</p>
        ) : (
          <>
            <SegmentedControl
              label={copy.title}
              name="map-topic"
              value={topic.topic.id}
              onChange={(id) => {
                setTopicId(id)
                setConceptId(undefined)
              }}
              options={map.topics.map((entry) => ({ value: entry.topic.id, label: entry.topic.name }))}
              className="justify-self-start"
            />
            <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
              <span className="font-medium">
                {topic.mastery == null ? t.practice.topics.notPlayed : copy.mastery(Math.round(topic.mastery))}
              </span>
              <span className="font-mono text-xs text-muted-foreground">{copy.completion(Math.round(topic.completion))}</span>
              {topic.calibrationAvailable && (
                <Button
                  size="sm"
                  variant="outline"
                  className="ml-auto"
                  aria-disabled={starting}
                  onClick={() => start(() => api.startCalibration({ topicId: topic.topic.id }))}
                >
                  <Crosshair aria-hidden="true" />
                  {copy.calibrate}
                </Button>
              )}
            </div>
            <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
              {topic.concepts.map((node) => (
                <li key={node.concept.id}>
                  <button
                    type="button"
                    aria-pressed={node.concept.id === conceptId}
                    onClick={() => setConceptId(node.concept.id === conceptId ? undefined : node.concept.id)}
                    className={cn(
                      'grid h-full min-h-20 w-full content-between gap-2 rounded-lg p-3 text-left ring-1 ring-inset transition-[box-shadow,scale] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring active:scale-[0.98]',
                      node.concept.id === conceptId
                        ? 'bg-primary/10 ring-2 ring-primary'
                        : 'bg-secondary ring-border hover:ring-input',
                      node.state === 'unknown' && 'text-muted-foreground',
                    )}
                  >
                    <span className="text-sm leading-snug font-medium text-pretty">{node.concept.name}</span>
                    <StateLabel node={node} />
                  </button>
                </li>
              ))}
            </ul>
            {selected && (
              <ConceptDetail
                node={selected}
                starting={starting}
                error={error}
                onPractice={() =>
                  start(() => api.startPractice({ topicId: topic.topic.id, conceptIds: [selected.concept.id], timed: false }))
                }
              />
            )}
            <ul className="flex flex-wrap gap-x-4 gap-y-1.5 text-xs text-muted-foreground">
              {ORDER.map((state) => (
                <li key={state} className="flex items-center gap-1.5">
                  <span aria-hidden="true" className={cn('size-2.5 rounded-full', STATES[state].dot)} />
                  {copy.states[state]}
                </li>
              ))}
            </ul>
          </>
        )}
      </CardContent>
    </Card>
  )
}

function StateLabel({ node }: { node: ConceptNode }) {
  const { t } = useLocale()
  return (
    <span className="flex items-center gap-1.5 text-xs">
      {node.state === 'graduated' ? (
        <Star aria-hidden="true" className="size-3 fill-primary text-primary" />
      ) : (
        <span aria-hidden="true" className={cn('size-2 rounded-full', STATES[node.state].dot)} />
      )}
      {t.profile.map.states[node.state]}
      {node.state !== 'unknown' && <span className="ml-auto font-mono">{Math.round(node.mastery)}%</span>}
    </span>
  )
}

interface ConceptDetailProps {
  node: ConceptNode
  starting: boolean
  error?: string
  onPractice: () => void
}

function ConceptDetail({ node, starting, error, onPractice }: ConceptDetailProps) {
  const { t, locale } = useLocale()
  const copy = t.profile.map
  const [openAnswer, setOpenAnswer] = useState<string>()

  return (
    <section aria-label={node.concept.name} className="grid gap-4 rounded-xl bg-secondary/60 p-4 ring-1 ring-border">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <h3 className="font-heading text-lg font-semibold">{node.concept.name}</h3>
        <StateLabel node={node} />
        {node.reviewDueAt && (
          <span className="text-xs text-muted-foreground">{copy.reviewDue(relativeTime(node.reviewDueAt, locale))}</span>
        )}
        <Button size="sm" className="ml-auto" aria-disabled={starting} onClick={() => !starting && onPractice()}>
          <Target aria-hidden="true" />
          {copy.practice}
        </Button>
      </div>
      <div className="grid gap-2">
        <p className="text-xs text-muted-foreground">{copy.lastAnswers}</p>
        {node.lastAnswers.length ? (
          <ul className="grid gap-1">
            {node.lastAnswers.map((answer) => {
              const key = `${answer.questionId}-${answer.at}`
              return (
                <li key={key} className="grid gap-2">
                  <button
                    type="button"
                    aria-expanded={openAnswer === key}
                    onClick={() => setOpenAnswer(openAnswer === key ? undefined : key)}
                    className="flex items-start gap-2.5 rounded-md p-1.5 text-left text-sm transition-colors hover:bg-secondary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                  >
                    {answer.isCorrect ? (
                      <Check aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-success" />
                    ) : (
                      <X aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-destructive" />
                    )}
                    <span className="sr-only">{answer.isCorrect ? copy.correct : copy.wrong}:</span>
                    <span className="line-clamp-2 flex-1 text-pretty">{answer.prompt}</span>
                    <span className="shrink-0 font-mono text-xs text-muted-foreground">{relativeTime(answer.at, locale)}</span>
                  </button>
                  {openAnswer === key && <AnsweredQuestion questionId={answer.questionId} />}
                </li>
              )
            })}
          </ul>
        ) : (
          <p className="text-sm text-muted-foreground">{copy.noAnswers}</p>
        )}
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </section>
  )
}

// One of my last answers opened up: the question with its key (I answered it, so the API includes the key).
function AnsweredQuestion({ questionId }: { questionId: string }) {
  const { t } = useLocale()
  const load = useCallback((signal: AbortSignal) => api.question(questionId, signal), [questionId])
  const { data, error } = useAsync(load)

  if (error)
    return (
      <p role="alert" className="text-sm text-destructive">
        {error.message}
      </p>
    )
  if (!data) return <Skeleton aria-busy="true" className="h-40 rounded-lg" />
  return (
    <div className="grid gap-3 rounded-lg bg-card p-4 ring-1 ring-border">
      <QuestionBody content={data.content} language={data.language} correct={data.key?.answer} locked onAnswer={() => {}} />
      {data.key && (
        <p className="text-sm text-pretty">
          <span className="font-medium text-primary">{t.match.remember}:</span> {data.key.remember}
        </p>
      )}
    </div>
  )
}
