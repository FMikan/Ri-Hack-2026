import { Bot } from 'lucide-react'
import { useState, type SubmitEvent } from 'react'
import { Link } from 'react-router'
import { choiceTile } from '@/components/shared/choice-tile'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import { cn } from '@/lib/utils'
import { useStartMatch } from '@/features/match/use-start-match'
import type { BotDto, BotPersona, MeDto, TopicDto } from '@/types/api'

const loadPlay = (signal: AbortSignal) => Promise.all([api.bots(signal), api.me(signal), api.listTopics(signal)])

// Pick a bot and a topic, then start: the server answers with the new match and the duel screen takes over.
export function PlayPage() {
  const { t } = useLocale()
  const play = useAsync(loadPlay)

  return (
    <main className="mx-auto grid w-full max-w-7xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${t.play.documentTitle} · StackOff`}</title>
      <header className="grid gap-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{t.play.title}</h1>
        <p className="text-sm text-pretty text-muted-foreground">{t.play.subtitle}</p>
      </header>
      {play.data ? (
        <PlayForm bots={play.data[0]} me={play.data[1]} topics={play.data[2]} />
      ) : play.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{t.play.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{play.error.message}</p>
            <Button variant="outline" onClick={play.reload}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div aria-busy="true" className="grid gap-5">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          <Skeleton className="h-56 rounded-xl" />
          <Skeleton className="h-40 rounded-xl" />
        </div>
      )}
    </main>
  )
}

function PlayForm({ bots, me, topics }: { bots: BotDto[]; me: MeDto; topics: TopicDto[] }) {
  const { t } = useLocale()
  const mine = new Set(me.activeTopicIds)
  const ordered = [...topics].sort((a, b) => Number(mine.has(b.id)) - Number(mine.has(a.id)))
  const [persona, setPersona] = useState<BotPersona | undefined>(
    () => (bots.find((bot) => bot.recommended) ?? bots[0])?.persona,
  )
  const [topicId, setTopicId] = useState<string | undefined>(() => ordered[0]?.id)
  const { start, starting, error } = useStartMatch()

  function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (persona && topicId) start(() => api.startBotMatch({ persona, topicId }))
  }

  return (
    <form className="grid gap-5" onSubmit={submit}>
      {me.activeMatchId && (
        <Card className="ring-2 ring-primary">
          <CardContent className="flex flex-wrap items-center justify-between gap-3">
            <p className="font-medium">{t.play.inMatch}</p>
            <Button asChild>
              <Link to={`/match/${me.activeMatchId}`}>{t.play.resume}</Link>
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>
            <h2 id="opponent-label">{t.play.opponent}</h2>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div role="radiogroup" aria-labelledby="opponent-label" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {bots.map((bot) => (
              <label key={bot.persona} className={cn(choiceTile, 'items-start')}>
                <input
                  type="radio"
                  name="persona"
                  value={bot.persona}
                  checked={persona === bot.persona}
                  onChange={() => setPersona(bot.persona)}
                  className="sr-only"
                />
                <span className="grid size-11 shrink-0 place-items-center rounded-full bg-primary/15 text-primary">
                  <Bot aria-hidden="true" className="size-5" />
                </span>
                <span className="grid min-w-0 gap-0.5">
                  <span className="font-heading font-semibold">{t.bots[bot.persona]}</span>
                  <span className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-xs text-muted-foreground">{bot.displayRating}</span>
                    {bot.recommended && <Badge variant="success">{t.play.recommended}</Badge>}
                  </span>
                  <span className="text-sm text-muted-foreground">{t.play.traits[bot.persona]}</span>
                </span>
              </label>
            ))}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>
            <h2 id="topic-label">{t.play.topic}</h2>
          </CardTitle>
        </CardHeader>
        <CardContent>
          {ordered.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t.play.noTopics}</p>
          ) : (
            <div role="radiogroup" aria-labelledby="topic-label" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              {ordered.map((topic) => {
                const Icon = topicIcon(topic.slug)
                return (
                  <label key={topic.id} className={cn(choiceTile, 'items-center')}>
                    <input
                      type="radio"
                      name="topic"
                      value={topic.id}
                      checked={topicId === topic.id}
                      onChange={() => setTopicId(topic.id)}
                      className="sr-only"
                    />
                    <Icon aria-hidden="true" className="size-5 shrink-0 text-primary" />
                    <span className="min-w-0 flex-1 font-medium">{topic.name}</span>
                    {mine.has(topic.id) && <Badge variant="outline">{t.play.yours}</Badge>}
                  </label>
                )
              })}
            </div>
          )}
        </CardContent>
      </Card>

      <div className="flex flex-wrap items-center justify-end gap-3">
        {error && (
          <p role="alert" className="mr-auto text-sm text-destructive">
            {error}
          </p>
        )}
        <Button type="submit" size="lg" aria-disabled={starting || !persona || !topicId} className="w-full sm:w-auto">
          {starting ? t.play.starting : t.play.start}
        </Button>
      </div>
    </form>
  )
}
