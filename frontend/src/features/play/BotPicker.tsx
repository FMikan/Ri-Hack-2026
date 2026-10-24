import { createElement, useState, type SubmitEvent } from 'react'
import { BotAvatar } from '@/components/shared/BotAvatar'
import { choiceTile } from '@/components/shared/choice-tile'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useStartMatch } from '@/features/match/use-start-match'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { demoBots } from '@/lib/fixtures/demo'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import { cn } from '@/lib/utils'
import type { BotDto, BotPersona, MeDto, TopicDto } from '@/types/api'
import { dialogFooter } from './dialog-footer'
import { DialogHeading, ResumeBanner } from './dialog-parts'

const loadBots = (signal: AbortSignal) => Promise.all([api.bots(signal), api.me(signal), api.listTopics(signal)])
const label = 'text-sm font-medium text-muted-foreground'

// Bot duel: pick a bot, then a topic below, then start; the duel screen takes over.
export function BotPicker() {
  const { t } = useLocale()
  const data = useAsync(loadBots)

  return (
    <>
      <DialogHeading title={t.play.title} description={t.play.subtitle} />
      {data.data ? (
        <BotForm bots={demoBots(data.data[0], data.data[1])} me={data.data[1]} topics={data.data[2]} />
      ) : data.error ? (
        <div role="alert" className="grid justify-items-start gap-3">
          <p>{t.play.loadFailed}</p>
          <p className="text-sm text-muted-foreground">{data.error.message}</p>
          <Button variant="outline" onClick={data.reload}>
            {t.common.retry}
          </Button>
        </div>
      ) : (
        <div aria-busy="true" className="grid gap-3 sm:grid-cols-2">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          {[0, 1, 2, 3].map((index) => (
            <Skeleton key={index} className="h-24 rounded-lg" />
          ))}
        </div>
      )}
    </>
  )
}

function BotForm({ bots, me, topics }: { bots: BotDto[]; me: MeDto; topics: TopicDto[] }) {
  const { t } = useLocale()
  const mine = new Set(me.activeTopicIds)
  const ordered = [...topics].sort((a, b) => Number(mine.has(b.id)) - Number(mine.has(a.id)))
  const [persona, setPersona] = useState<BotPersona | undefined>(() => (bots.find((bot) => bot.recommended) ?? bots[0])?.persona)
  const [topicId, setTopicId] = useState<string | undefined>(() => ordered[0]?.id)
  const { start, starting, error } = useStartMatch()

  function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (persona && topicId) start(() => api.startBotMatch({ persona, topicId }))
  }

  return (
    <form className="grid gap-5" onSubmit={submit}>
      {me.activeMatchId && <ResumeBanner matchId={me.activeMatchId} />}

      <section className="grid gap-2.5">
        <h3 id="bot-label" className={label}>
          {t.play.opponent}
        </h3>
        <div role="radiogroup" aria-labelledby="bot-label" className="grid gap-3 sm:grid-cols-2">
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
              <BotAvatar persona={bot.persona} className="size-14" />
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
      </section>

      <section className="grid gap-2.5">
        <h3 id="bot-topic-label" className={label}>
          {t.play.topic}
        </h3>
        {ordered.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t.play.noTopics}</p>
        ) : (
          <div role="radiogroup" aria-labelledby="bot-topic-label" className="grid gap-3 sm:grid-cols-2">
            {ordered.map((topic) => (
              <label key={topic.id} className={cn(choiceTile, 'items-center')}>
                <input
                  type="radio"
                  name="topic"
                  value={topic.id}
                  checked={topicId === topic.id}
                  onChange={() => setTopicId(topic.id)}
                  className="sr-only"
                />
                {createElement(topicIcon(topic.slug), { 'aria-hidden': true, className: 'size-5 shrink-0 text-primary' })}
                <span className="min-w-0 flex-1 font-medium">{topic.name}</span>
                {mine.has(topic.id) && <Badge variant="outline">{t.play.yours}</Badge>}
              </label>
            ))}
          </div>
        )}
      </section>

      <div className={dialogFooter}>
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
