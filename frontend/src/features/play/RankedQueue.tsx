import { createElement, useState } from 'react'
import { Link } from 'react-router'
import { DuelBadge } from '@/components/shared/RankedDuelIcon'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DialogClose } from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
import { useAsync } from '@/hooks/use-async'
import { useTicker } from '@/hooks/use-ticker'
import { api } from '@/lib/api-client'
import { demoMe } from '@/lib/fixtures/demo'
import { demoQueue } from '@/lib/fixtures/queue'
import { MIN_TOPICS } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import type { MeDto, TopicDto } from '@/types/api'
import { dialogFooter } from './dialog-footer'
import { DialogHeading, ResumeBanner } from './dialog-parts'

const loadQueue = (signal: AbortSignal) => Promise.all([api.me(signal), api.listTopics(signal)])

// Ranked duel: opening the dialog puts the player in the queue; closing it leaves the queue (game idea §4).
export function RankedQueue({ onPlayBot }: { onPlayBot: () => void }) {
  const { t } = useLocale()
  const data = useAsync(loadQueue)

  return (
    <>
      <DialogHeading title={t.queue.title} description={t.queue.searching} />
      {data.data ? (
        <Queue me={demoMe(data.data[0])} topics={data.data[1]} onPlayBot={onPlayBot} />
      ) : data.error ? (
        <div role="alert" className="grid justify-items-start gap-3">
          <p>{t.play.loadFailed}</p>
          <p className="text-sm text-muted-foreground">{data.error.message}</p>
          <Button variant="outline" onClick={data.reload}>
            {t.common.retry}
          </Button>
        </div>
      ) : (
        <Skeleton aria-busy="true" className="h-56 rounded-lg" />
      )}
    </>
  )
}

const clock = (ms: number) => {
  const seconds = Math.floor(ms / 1000)
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`
}

function Queue({ me, topics, onPlayBot }: { me: MeDto; topics: TopicDto[]; onPlayBot: () => void }) {
  const { t } = useLocale()
  const copy = t.queue
  const [enteredAt] = useState(() => performance.now())
  const [keepWaiting, setKeepWaiting] = useState(false)
  const locked = me.activeTopicIds.length < MIN_TOPICS
  const now = useTicker(!locked && !me.activeMatchId, 1000)
  const waited = Math.max(0, now - enteredAt)
  const queue = demoQueue(waited, me.activeTopicIds)
  const range = queue.rangeNow ?? 0
  const mine = topics.filter((topic) => me.activeTopicIds.includes(topic.id))

  if (me.activeMatchId) return <ResumeBanner matchId={me.activeMatchId} />
  if (locked)
    return (
      <div className="grid justify-items-start gap-3">
        <p>{copy.locked}</p>
        <Button asChild variant="outline">
          <Link to="/#topics">{copy.pickTopics}</Link>
        </Button>
      </div>
    )

  return (
    <div className="grid gap-6">
      <div className="grid justify-items-center gap-3 pt-2 text-center">
        <span className="relative grid size-24 place-items-center">
          <span aria-hidden="true" className="absolute inset-2 animate-ping rounded-full bg-primary/25 motion-reduce:hidden" />
          <DuelBadge className="relative size-20" />
        </span>
        <p className="text-sm text-muted-foreground">{copy.elapsed}</p>
        <p role="timer" className="font-heading text-5xl font-semibold tabular-nums">
          {clock(waited)}
        </p>
        <Badge variant="secondary" className="font-mono">
          {copy.range(me.rating - range, me.rating + range)}
        </Badge>
      </div>

      <section className="grid justify-items-center gap-2.5">
        <h3 className="text-sm font-medium text-muted-foreground">{copy.topics}</h3>
        <ul className="flex flex-wrap justify-center gap-2">
          {mine.map((topic) => (
            <li key={topic.id}>
              <Badge variant="outline" className="h-8 gap-1.5 px-3 text-sm">
                {createElement(topicIcon(topic.slug), { 'aria-hidden': true, className: 'text-primary' })}
                {topic.name}
              </Badge>
            </li>
          ))}
        </ul>
      </section>

      {queue.botOffer && !keepWaiting && (
        <div aria-live="polite" className="grid gap-3 rounded-lg bg-secondary p-4 ring-1 ring-border ring-inset sm:flex sm:items-center">
          <p className="flex-1 text-sm text-pretty">{copy.noOpponent}</p>
          <div className="flex flex-wrap gap-2">
            <Button onClick={onPlayBot}>{copy.playBot}</Button>
            <Button variant="outline" onClick={() => setKeepWaiting(true)}>
              {copy.keepWaiting}
            </Button>
          </div>
        </div>
      )}

      <div className={dialogFooter}>
        {keepWaiting && (
          <Button variant="ghost" className="mr-auto" onClick={onPlayBot}>
            {copy.playBot}
          </Button>
        )}
        <DialogClose asChild>
          <Button variant="outline" size="lg" className="w-full sm:w-auto">
            {copy.cancel}
          </Button>
        </DialogClose>
      </div>
    </div>
  )
}
