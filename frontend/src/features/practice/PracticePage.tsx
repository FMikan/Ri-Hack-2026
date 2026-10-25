import { RotateCcw } from 'lucide-react'
import { createElement, useState, type SubmitEvent } from 'react'
import { choiceTile } from '@/components/shared/choice-tile'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { TodayCard } from '@/features/dashboard/TodayCard'
import { useStartMatch } from '@/features/match/use-start-match'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import { cn } from '@/lib/utils'
import type { DueConceptsDto, KnowledgeMapDto, MeDto } from '@/types/api'

type Mode = 'untimed' | 'timed'

const loadPractice = (signal: AbortSignal) =>
  Promise.all([api.dueConcepts(signal), api.knowledgeMap(signal), api.me(signal)])

// Practice without an opponent: due reviews first, then any topic. Untimed or timed, chosen once for both.
export function PracticePage() {
  const { t } = useLocale()
  const practice = useAsync(loadPractice)

  return (
    <main className="mx-auto grid w-full max-w-7xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${t.practice.documentTitle} · StackOff`}</title>
      <header className="grid gap-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{t.practice.title}</h1>
        <p className="text-sm text-pretty text-muted-foreground">{t.practice.subtitle}</p>
      </header>
      <TodaySection />
      {practice.data ? (
        <PracticeForm due={practice.data[0]} map={practice.data[1]} me={practice.data[2]} />
      ) : practice.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{t.practice.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{practice.error.message}</p>
            <Button variant="outline" onClick={practice.reload}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div aria-busy="true" className="grid gap-5 xl:grid-cols-2">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          <Skeleton className="h-72 rounded-xl" />
          <Skeleton className="h-72 rounded-xl" />
        </div>
      )}
    </main>
  )
}

// Today's tasks and the daily challenge, moved here from the dashboard. They come from the dashboard read: the
// challenge's own GET would open the attempt and start its 60 s clock.
function TodaySection() {
  const { t } = useLocale()
  const today = useAsync(api.dashboard)

  if (today.data) return today.data.dailyTasks.length > 0 ? <TodayCard data={today.data} /> : null
  if (today.error)
    return (
      <Card role="alert">
        <CardContent className="grid justify-items-start gap-3">
          <p>{t.practice.todayFailed}</p>
          <p className="text-sm text-muted-foreground">{today.error.message}</p>
          <Button variant="outline" onClick={today.reload}>
            {t.common.retry}
          </Button>
        </CardContent>
      </Card>
    )
  return <Skeleton aria-busy="true" className="h-44 rounded-xl" />
}

interface PracticeFormProps {
  due: DueConceptsDto
  map: KnowledgeMapDto
  me: MeDto
}

function PracticeForm({ due, map, me }: PracticeFormProps) {
  const { t, locale } = useLocale()
  const copy = t.practice
  const mine = new Set(me.activeTopicIds)
  const topics = [...map.topics].sort((a, b) => Number(mine.has(b.topic.id)) - Number(mine.has(a.topic.id)))
  const [mode, setMode] = useState<Mode>('untimed')
  const [topicId, setTopicId] = useState(() => topics[0]?.topic.id)
  const { start, starting, error } = useStartMatch()
  const timed = mode === 'timed'

  // Due concepts grouped by topic: one practice per topic, those concepts first.
  const groups = Object.values(
    due.concepts.reduce<Record<string, DueConceptsDto['concepts']>>((byTopic, concept) => {
      ;(byTopic[concept.topic.id] ??= []).push(concept)
      return byTopic
    }, {}),
  )

  function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (topicId) start(() => api.startPractice({ topicId, timed }))
  }

  const dueCard = (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2>{copy.due.title}</h2>
        </CardTitle>
        <CardDescription>{groups.length ? copy.due.subtitle : copy.due.empty}</CardDescription>
      </CardHeader>
      {groups.length > 0 && (
        <CardContent className="grid gap-4">
          {groups.map((concepts) => {
            const { topic } = concepts[0]
            return (
              <section key={topic.id} className="grid gap-2">
                <div className="flex flex-wrap items-center gap-3">
                  <h3 className="flex items-center gap-2 font-medium">
                    {createElement(topicIcon(topic.slug), { 'aria-hidden': true, className: 'size-4 text-primary' })}
                    {topic.name}
                  </h3>
                  <Button
                    size="sm"
                    variant="outline"
                    className="ml-auto"
                    aria-disabled={starting}
                    onClick={() =>
                      start(() => api.startPractice({ topicId: topic.id, timed, conceptIds: concepts.map((concept) => concept.id) }))
                    }
                  >
                    <RotateCcw aria-hidden="true" />
                    {copy.due.review}
                  </Button>
                </div>
                <ul className="grid gap-1.5">
                  {concepts.map((concept) => (
                    <li key={concept.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg px-3 py-2.5 text-sm ring-1 ring-border ring-inset">
                      <span className="font-medium">{concept.name}</span>
                      <span className="ml-auto font-mono text-xs text-muted-foreground">
                        {copy.due.step(concept.reviewStep)} · {relativeTime(concept.reviewDueAt, locale)}
                      </span>
                    </li>
                  ))}
                </ul>
              </section>
            )
          })}
        </CardContent>
      )}
    </Card>
  )

  return (
    <form className="grid gap-5" onSubmit={submit}>
      <div className="grid gap-2">
        <SegmentedControl
          label={copy.mode}
          name="mode"
          value={mode}
          onChange={setMode}
          toggle
          options={[
            { value: 'untimed', label: copy.modes.untimed },
            { value: 'timed', label: copy.modes.timed },
          ]}
          className="justify-self-start"
        />
        <p className="text-sm text-muted-foreground">{copy.modeHints[mode]}</p>
      </div>

      <div className={cn('grid items-start gap-5', groups.length > 0 && 'xl:grid-cols-2')}>
        {/* Due reviews come first when there are any (game idea §11). */}
        {groups.length > 0 && dueCard}
        <Card>
          <CardHeader>
            <CardTitle>
              <h2 id="practice-topic">{copy.topics.title}</h2>
            </CardTitle>
            <CardDescription>{copy.topics.subtitle}</CardDescription>
          </CardHeader>
          <CardContent>
            <div
              role="radiogroup"
              aria-labelledby="practice-topic"
              className={cn('grid gap-3 sm:grid-cols-2', groups.length === 0 && 'xl:grid-cols-4')}
            >
              {topics.map(({ topic, mastery, concepts }) => {
                const weak = concepts.filter((concept) => concept.state === 'weak').length
                return (
                  <label key={topic.id} className={cn(choiceTile, 'items-start')}>
                    <input
                      type="radio"
                      name="topic"
                      value={topic.id}
                      checked={topicId === topic.id}
                      onChange={() => setTopicId(topic.id)}
                      className="sr-only"
                    />
                    {createElement(topicIcon(topic.slug), { 'aria-hidden': true, className: 'mt-0.5 size-5 shrink-0 text-primary' })}
                    <span className="grid min-w-0 flex-1 gap-1">
                      <span className="font-medium">{topic.name}</span>
                      <span className="font-mono text-xs text-muted-foreground">
                        {mastery == null ? copy.topics.notPlayed : `${Math.round(mastery)}%`}
                        {weak > 0 && <span className="text-destructive"> · {copy.topics.weak(weak)}</span>}
                      </span>
                    </span>
                    {mine.has(topic.id) && <Badge variant="outline">{t.play.yours}</Badge>}
                  </label>
                )
              })}
            </div>
          </CardContent>
        </Card>
        {groups.length === 0 && dueCard}
      </div>

      <div className="flex flex-wrap items-center justify-end gap-3">
        {error && (
          <p role="alert" className="mr-auto text-sm text-destructive">
            {error}
          </p>
        )}
        <Button type="submit" size="lg" aria-disabled={starting || !topicId} className="w-full sm:w-auto">
          {starting ? copy.starting : copy.start}
        </Button>
      </div>
    </form>
  )
}
