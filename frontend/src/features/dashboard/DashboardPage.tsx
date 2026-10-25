import { useEffect } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { usePoll } from '@/hooks/use-poll'
import { api } from '@/lib/api-client'
import { MIN_TOPICS } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import type { DashboardDto } from '@/types/api'
import { Inbox } from './Inbox'
import { PlayCard } from './PlayCard'
import { LevelTile, TierTile } from './ProgressTiles'
import { RatingCard } from './RatingCard'
import { SkillGraphCard } from './SkillGraphCard'
import { TodayNotice } from './TodayNotice'
import { TopicsCard } from './TopicsCard'

// xl: three columns. Play spans the first two with tier and level under it, rating fills the right column,
// the skill graph (wide) and topics form the bottom row. Below xl the cards stack in DOM order.
const GRID = 'grid grid-cols-2 gap-4 lg:gap-5 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1.15fr)]'

// Polled, so a rematch or challenge someone sends shows up without a reload (it also keeps "online" fresh, docs §6.11).
const keepPolling = () => false

export function DashboardPage() {
  const { t } = useLocale()
  const dashboard = usePoll(api.dashboard, 5000, keepPolling)

  return (
    <main className="mx-auto grid w-full max-w-7xl gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${t.dashboard.documentTitle} · StackOff`}</title>
      {dashboard.data ? (
        <DashboardView data={dashboard.data} reload={dashboard.refresh} />
      ) : dashboard.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{t.dashboard.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{dashboard.error.message}</p>
            <Button variant="outline" onClick={dashboard.refresh}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <DashboardSkeleton />
      )}
    </main>
  )
}

function DashboardView({ data, reload }: { data: DashboardDto; reload: () => void }) {
  const { t } = useLocale()
  const { me } = data
  const { setUser } = useAuth()
  const needsTopics = me.activeTopicIds.length < MIN_TOPICS
  // The top bar shows coins from the session; the poll keeps them current after duels, lessons and tasks.
  useEffect(() => setUser(me), [me, setUser])
  const topics = (
    <TopicsCard
      id="topics"
      topics={data.topics}
      queueLocked={data.queueLocked}
      onSaved={reload}
      className="col-span-2 xl:col-span-1 xl:col-start-3 xl:row-start-3"
    />
  )

  return (
    <>
      {/* The greeting sits in the top bar; the page keeps its heading for screen readers. */}
      <h1 className="sr-only">{t.dashboard.documentTitle}</h1>
      <Inbox inbox={data.inbox} />
      {!needsTopics && <TodayNotice data={data} />}
      <div className={GRID}>
        <PlayCard me={me} locked={needsTopics} className="col-span-2 xl:col-start-1 xl:row-start-1" />
        {/* A new player picks topics right after the play card; everyone else sees them further down. */}
        {needsTopics && topics}
        <RatingCard
          me={me}
          history={data.ratingHistory}
          className="col-span-2 xl:col-span-1 xl:col-start-3 xl:row-span-2 xl:row-start-1"
        />
        <div className="contents xl:*:row-start-2">
          <TierTile me={me} />
          <LevelTile me={me} />
        </div>
        {!needsTopics && topics}
        {/* Only the picked topics; a new player without picks still sees them all instead of an empty card. */}
        <SkillGraphCard
          topics={needsTopics ? data.topics : data.topics.filter((topic) => topic.active)}
          className="col-span-2 xl:col-start-1 xl:row-start-3"
        />

      </div>
    </>
  )
}

function DashboardSkeleton() {
  const { t } = useLocale()

  return (
    <div aria-busy="true" className="grid gap-5">
      <p role="status" className="sr-only">
        {t.common.loading}
      </p>
      <div className={GRID}>
        <Skeleton className="col-span-2 h-60 rounded-xl" />
        <Skeleton className="col-span-2 h-60 rounded-xl xl:col-span-1 xl:row-span-2" />
        <Skeleton className="h-44 rounded-xl" />
        <Skeleton className="h-44 rounded-xl" />
      </div>
    </div>
  )
}
