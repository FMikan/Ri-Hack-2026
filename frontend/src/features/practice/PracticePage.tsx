import { ArrowRight, CalendarCheck, RotateCcw, Timer, TimerOff } from 'lucide-react'
import { createElement, useState, type SubmitEvent } from 'react'
import { choiceTile } from '@/components/shared/choice-tile'
import { Meter } from '@/components/shared/Meter'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
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

const REVIEW_STEPS = [1, 2, 3, 4, 5] // reviews after 1, 3, 7, 14 and 30 days

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
        // Shaped like the page it stands in for: mode row, the review strip, twelve topic tiles.
        <div aria-busy="true" className="grid gap-5">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          <Skeleton className="h-11 w-56 rounded-lg" />
          <Skeleton className="h-16 rounded-xl" />
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {Array.from({ length: 12 }, (_, index) => (
              <Skeleton key={index} className="h-28 rounded-lg" />
            ))}
          </div>
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
  const selected = topics.find(({ topic }) => topic.id === topicId)?.topic

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

  // Nothing due: a slim strip instead of a card holding one sentence. Either way it comes first (game idea §11).
  const dueCard = groups.length ? (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2>{copy.due.title}</h2>
        </CardTitle>
        <CardDescription>{copy.due.subtitle}</CardDescription>
      </CardHeader>
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
              <ul className="divide-y divide-border rounded-lg ring-1 ring-border ring-inset">
                {concepts.map((concept) => (
                  <li key={concept.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2.5 text-sm">
                    <span className="font-medium">{concept.name}</span>
                    <span className="ml-auto flex items-center gap-2 font-mono text-xs text-muted-foreground">
                      <span aria-hidden="true" className="flex gap-0.5">
                        {REVIEW_STEPS.map((step) => (
                          <span
                            key={step}
                            className={cn('h-1.5 w-3 rounded-full', step <= concept.reviewStep ? 'bg-success' : 'bg-foreground/15')}
                          />
                        ))}
                      </span>
                      {copy.due.step(concept.reviewStep)} · {relativeTime(concept.reviewDueAt, locale)}
                    </span>
                  </li>
                ))}
              </ul>
            </section>
          )
        })}
      </CardContent>
    </Card>
  ) : (
    <Card size="sm" className="flex-row items-center gap-3 px-4">
      <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-success/10 text-success">
        <CalendarCheck aria-hidden="true" className="size-4" />
      </span>
      <div className="grid min-w-0 gap-0.5">
        <h2 className="font-heading text-base font-semibold">{copy.due.title}</h2>
        <p className="text-sm text-pretty text-muted-foreground">{copy.due.empty}</p>
      </div>
    </Card>
  )

  return (
    <form className="grid gap-5" onSubmit={submit}>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <span aria-hidden="true" className="font-mono text-xs tracking-wide text-muted-foreground uppercase">
          {copy.mode}
        </span>
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
        />
        <p aria-live="polite" className="flex items-center gap-1.5 text-xs text-pretty text-muted-foreground">
          {createElement(timed ? Timer : TimerOff, { 'aria-hidden': true, className: 'size-3.5 shrink-0' })}
          {copy.modeHints[mode]}
        </p>
      </div>

      <div className={cn('grid items-start gap-5', groups.length > 0 && 'xl:grid-cols-2')}>
        {dueCard}
        {/* overflow-clip, not hidden: the footer sticks to the bottom of the screen on phones. */}
        <Card className="overflow-clip">
          <CardHeader>
            <CardTitle>
              <h2 id="practice-topic">{copy.topics.title}</h2>
            </CardTitle>
            <CardDescription>{copy.topics.subtitle}</CardDescription>
          </CardHeader>
          <CardContent>
            {topics.length === 0 ? (
              <p className="text-sm text-muted-foreground">{copy.topics.empty}</p>
            ) : (
              <div
                role="radiogroup"
                aria-labelledby="practice-topic"
                className={cn('grid auto-rows-fr gap-3 sm:grid-cols-2', groups.length === 0 && 'xl:grid-cols-4')}
              >
                {topics.map(({ topic, mastery, concepts }) => {
                  const weak = concepts.filter((concept) => concept.state === 'weak').length
                  const checked = topicId === topic.id
                  return (
                    <label key={topic.id} className={cn(choiceTile, 'flex-col')}>
                      <input
                        type="radio"
                        name="topic"
                        value={topic.id}
                        checked={checked}
                        onChange={() => setTopicId(topic.id)}
                        className="sr-only"
                      />
                      <span className="flex items-start gap-3">
                        {createElement(topicIcon(topic.slug), {
                          'aria-hidden': true,
                          className: cn('mt-0.5 size-5 shrink-0 transition-colors', checked ? 'text-primary' : 'text-muted-foreground'),
                        })}
                        <span className="min-w-0 flex-1 font-medium text-balance">{topic.name}</span>
                        <RadioMark checked={checked} />
                      </span>
                      <span className="mt-auto grid gap-2">
                        <span className="flex flex-wrap items-center gap-2 font-mono text-xs tabular-nums">
                          <span className={mastery == null ? 'text-muted-foreground' : 'text-foreground'}>
                            {mastery == null ? copy.topics.notPlayed : `${Math.round(mastery)}%`}
                          </span>
                          {weak > 0 && <span className="rounded-md bg-destructive/15 px-1.5 py-0.5 text-destructive">{copy.topics.weak(weak)}</span>}
                          {mine.has(topic.id) && (
                            <Badge variant="outline" className="ml-auto font-mono uppercase">
                              {t.play.yours}
                            </Badge>
                          )}
                        </span>
                        {mastery == null ? (
                          <span aria-hidden="true" className="block h-1.5 rounded-full bg-background" />
                        ) : (
                          <Meter value={mastery / 100} label={t.profile.map.mastery(Math.round(mastery))} className="h-1.5 bg-background" />
                        )}
                      </span>
                    </label>
                  )
                })}
              </div>
            )}
          </CardContent>
          <CardFooter className="flex-wrap gap-3 max-sm:sticky max-sm:bottom-0 max-sm:z-10 max-sm:bg-card max-sm:pb-[max(--spacing(4),env(safe-area-inset-bottom))]">
            {error && (
              <p role="alert" className="basis-full text-sm text-destructive">
                {error}
              </p>
            )}
            {selected && (
              <p className="mr-auto min-w-0 text-sm">
                <span className="font-medium">{selected.name}</span>{' '}
                <span className="font-mono text-xs text-muted-foreground uppercase">· {copy.modes[mode]}</span>
              </p>
            )}
            <Button type="submit" size="lg" aria-disabled={starting || !topicId} className="ml-auto w-full aria-disabled:opacity-50 sm:w-auto">
              {starting ? copy.starting : copy.start}
              <ArrowRight data-icon="inline-end" aria-hidden="true" />
            </Button>
          </CardFooter>
        </Card>
      </div>
    </form>
  )
}

// The tile's radio, drawn: a ring that turns sky, and a dot that grows in when picked. The native input stays hidden.
function RadioMark({ checked }: { checked: boolean }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'mt-0.5 grid size-4 shrink-0 place-items-center rounded-full ring-2 transition-[box-shadow] duration-200 ring-inset motion-reduce:transition-none',
        checked ? 'ring-primary' : 'ring-input',
      )}
    >
      <span
        className={cn(
          'size-2 rounded-full bg-primary transition-[opacity,scale] duration-200 ease-[cubic-bezier(0.2,0,0,1)] motion-reduce:transition-none',
          checked ? 'scale-100 opacity-100' : 'scale-25 opacity-0',
        )}
      />
    </span>
  )
}
